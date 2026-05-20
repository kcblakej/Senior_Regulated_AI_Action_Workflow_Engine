using RegulatedAiWorkflow.Domain;

namespace RegulatedAiWorkflow.Workflow;

public static class ApprovalGate
{
    // Low and Medium do not require approval. Medium would be configurable in a real policy
    // engine; here it is hardcoded to keep the slice small. High risk is blocked unless an
    // approver is present and is not the requester (four-eyes). In production the approver
    // must also present a signed, single-use approval token bound to the action and a hash
    // of the risk assessment so an old approval cannot be replayed on a new request — see
    // PRODUCTION_NOTES.
    public static ApprovalDecision RequestOrVerifyApproval(
        string? requestedAction,
        RiskLevel riskLevel,
        string requestedBy,
        string? approvedBy)
    {
        if (string.IsNullOrWhiteSpace(requestedAction))
        {
            return ApprovalDecision.NotRequired;
        }

        if (riskLevel is RiskLevel.Low or RiskLevel.Medium)
        {
            return ApprovalDecision.NotRequired;
        }

        if (string.IsNullOrWhiteSpace(approvedBy))
        {
            return ApprovalDecision.Blocked;
        }

        // Four-eyes: the approver must be a different identity than the requester.
        if (string.Equals(approvedBy, requestedBy, StringComparison.OrdinalIgnoreCase))
        {
            return ApprovalDecision.Blocked;
        }

        return ApprovalDecision.Approved;
    }
}
