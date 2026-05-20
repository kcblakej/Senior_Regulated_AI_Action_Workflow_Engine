using RegulatedAiWorkflow.Contracts;
using RegulatedAiWorkflow.Data;
using RegulatedAiWorkflow.Domain;

namespace RegulatedAiWorkflow.Workflow;

public sealed class WorkflowOrchestrator
{
    private readonly IEvidenceRepository _evidence;
    private readonly IPolicyRepository _policies;
    private readonly IVendorRepository _vendors;
    private readonly IActionExecutor _actionExecutor;
    private readonly AuditWriter _audit;
    private readonly TimeProvider _time;

    public WorkflowOrchestrator(
        IEvidenceRepository evidence,
        IPolicyRepository policies,
        IVendorRepository vendors,
        IActionExecutor actionExecutor,
        AuditWriter audit,
        TimeProvider time)
    {
        _evidence = evidence;
        _policies = policies;
        _vendors = vendors;
        _actionExecutor = actionExecutor;
        _audit = audit;
        _time = time;
    }

    public Task<WorkflowResponse> RunAsync(WorkflowRequest request)
    {
        var correlationId = Guid.NewGuid().ToString();
        var inputHash = AuditWriter.HashInput(request.Question);
        var hasAction = !string.IsNullOrWhiteSpace(request.RequestedAction);

        Audit(correlationId, AuditEventType.WorkflowStarted, request, inputHash);

        // Both lookups are tenant-scoped inside the repository — defense in depth against a
        // future API layer that forgets to pass tenantId from the authenticated principal.
        var evidence = EvidenceSearcher.SearchEvidence(_evidence, request.TenantId, request.Question);
        var vendor = ResolveVendor(request.TenantId, request.Question);

        // Reads structured metadata only; the snippet text never reaches a decision branch.
        var assessment = RiskEvaluator.EvaluateRisk(evidence, vendor, _time.GetUtcNow());
        var evidenceIds = assessment.Citations.Select(c => c.DocumentId).ToArray();

        Audit(correlationId, AuditEventType.DecisionComputed, request, inputHash,
            assessment.RiskLevel, evidenceIds);

        var policies = _policies.GetForTenant(request.TenantId);

        // Role check first — applies even on low-risk vendors, and an approver cannot unblock
        // an RBAC failure. The audit trail records the attempt before the rejection.
        if (hasAction && !RoleAuthorizer.CanExecuteActions(request.Role))
        {
            Audit(correlationId, AuditEventType.ActionBlocked, request, inputHash,
                assessment.RiskLevel, evidenceIds);
            Audit(correlationId, AuditEventType.WorkflowCompleted, request, inputHash,
                assessment.RiskLevel, evidenceIds);
            return Task.FromResult(BuildResponse(assessment, policies,
                ActionStatus.BlockedUnauthorizedRole, requiresApproval: false, correlationId));
        }

        var approval = ApprovalGate.RequestOrVerifyApproval(
            request.RequestedAction, assessment.RiskLevel,
            request.UserId, request.ApprovedBy);

        // "Requires approval" is true whenever an action is in scope and the risk level
        // mandates one — independent of whether the current request supplied a valid approver.
        var requiresApproval = approval is ApprovalDecision.Blocked or ApprovalDecision.Approved;

        ActionStatus status;
        if (!hasAction)
        {
            status = ActionStatus.NoActionRequested;
        }
        else if (approval == ApprovalDecision.Blocked)
        {
            Audit(correlationId, AuditEventType.ActionBlocked, request, inputHash,
                assessment.RiskLevel, evidenceIds);
            status = ActionStatus.BlockedPendingApproval;
        }
        else
        {
            _actionExecutor.ExecuteMockAction(request.TenantId, request.RequestedAction!, vendor.Id);
            Audit(correlationId, AuditEventType.ActionExecuted, request, inputHash,
                assessment.RiskLevel, evidenceIds);
            status = ActionStatus.Executed;
        }

        Audit(correlationId, AuditEventType.WorkflowCompleted, request, inputHash,
            assessment.RiskLevel, evidenceIds);

        return Task.FromResult(BuildResponse(assessment, policies, status, requiresApproval, correlationId));
    }

    private Vendor ResolveVendor(string tenantId, string query)
    {
        // Naive keyword match against the vendor catalog. A real system would extract the
        // vendor reference from the question (NLP or structured form field) and reject
        // ambiguous matches rather than silently picking the first hit.
        var match = _vendors.GetForTenant(tenantId)
            .FirstOrDefault(v => query.Contains(v.Name, StringComparison.OrdinalIgnoreCase));

        // No match: fall through with a non-payment vendor so the risk evaluator still runs
        // a consistent code path. Only the contract rule will fire.
        return match ?? new Vendor("unknown", tenantId, "Unknown", ProcessesPaymentData: false);
    }

    private void Audit(
        string correlationId,
        AuditEventType type,
        WorkflowRequest request,
        string inputHash,
        RiskLevel? riskLevel = null,
        IReadOnlyList<string>? evidenceIds = null)
    {
        _audit.WriteAuditEvent(
            correlationId, type,
            request.TenantId, request.UserId, request.Role,
            inputHash,
            action: request.RequestedAction,
            riskLevel: riskLevel,
            evidenceIds: evidenceIds,
            approvedBy: request.ApprovedBy);
    }

    private static WorkflowResponse BuildResponse(
        RiskAssessment assessment,
        IReadOnlyList<Policy> policies,
        ActionStatus status,
        bool requiresApproval,
        string correlationId)
    {
        // Citations combine the evidence we inspected with the tenant policies that articulate
        // the rules. Both are presented to the human reader; neither was used for control flow.
        var citations = assessment.Citations
            .Concat(policies.Select(p => new Citation(p.Id, p.Body)))
            .ToList();

        return new WorkflowResponse(
            RiskLevel: assessment.RiskLevel,
            Recommendation: Display.Recommendation(assessment.RiskLevel),
            Reasons: assessment.Reasons.Select(Display.ForReason).ToList(),
            Citations: citations,
            MissingEvidence: assessment.MissingEvidence.Select(Display.ForEvidenceType).ToList(),
            RequiresApproval: requiresApproval,
            ActionStatus: status,
            CorrelationId: correlationId);
    }
}
