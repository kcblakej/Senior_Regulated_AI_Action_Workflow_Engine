using RegulatedAiWorkflow.Domain;

namespace RegulatedAiWorkflow.Workflow;

public sealed record RiskAssessment(
    RiskLevel RiskLevel,
    IReadOnlyList<RiskReason> Reasons,
    IReadOnlyList<Citation> Citations,
    IReadOnlyList<EvidenceType> MissingEvidence);
