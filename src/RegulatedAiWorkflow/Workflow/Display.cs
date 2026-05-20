using RegulatedAiWorkflow.Domain;

namespace RegulatedAiWorkflow.Workflow;

// Closed-enum-to-display-string mappings. The internal RiskAssessment carries enums; the
// wire contract carries strings (per the README example). Keeping the conversion in one
// place means the strings here are the only place display text lives.
internal static class Display
{
    public static string ForReason(RiskReason reason) => reason switch
    {
        RiskReason.MissingSoc2Report => "No SOC 2 evidence found.",
        RiskReason.Soc2ReportNotRecent => "SOC 2 report is older than 12 months.",
        RiskReason.ContractMissingBreachNotificationClause => "Contract lacks breach notification language.",
        _ => reason.ToString(),
    };

    public static string ForEvidenceType(EvidenceType type) => type switch
    {
        EvidenceType.Soc2Report => "SOC 2 report",
        EvidenceType.Contract => "contract",
        EvidenceType.DataRetentionPolicy => "data retention schedule",
        _ => type.ToString(),
    };

    public static string Recommendation(RiskLevel level) => level switch
    {
        RiskLevel.Low => "Looks fine.",
        RiskLevel.Medium => "Proceed with caution.",
        RiskLevel.High => "Do not approve yet.",
        _ => "Unknown.",
    };
}
