using RegulatedAiWorkflow.Domain;

namespace RegulatedAiWorkflow.Contracts;

public sealed record WorkflowResponse(
    RiskLevel RiskLevel,
    string Recommendation,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<Citation> Citations,
    IReadOnlyList<string> MissingEvidence,
    bool RequiresApproval,
    ActionStatus ActionStatus,
    string CorrelationId);
