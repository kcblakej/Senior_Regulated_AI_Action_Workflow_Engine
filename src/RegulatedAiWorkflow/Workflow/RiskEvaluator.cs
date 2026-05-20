using RegulatedAiWorkflow.Domain;

namespace RegulatedAiWorkflow.Workflow;

public static class RiskEvaluator
{
    // SOC 2 reports are considered "current" if issued within this window.
    private const int Soc2RecencyDays = 365;

    // Trust boundary: this function reads structured metadata only (Type, Source, IssuedDate,
    // HasBreachNotificationClause). The Evidence.Snippet field is never inspected to make a
    // decision — it flows through to Citations on the response untouched. A future LLM-backed
    // evaluator must preserve this invariant: the LLM proposes, this code disposes.
    public static RiskAssessment EvaluateRisk(
        IReadOnlyList<Evidence> evidence,
        Vendor vendor,
        DateTimeOffset asOf)
    {
        var reasons = new List<RiskReason>();
        var citations = new List<Citation>();
        var missing = new List<EvidenceType>();

        if (vendor.ProcessesPaymentData)
        {
            EvaluateSoc2(evidence, asOf, reasons, citations, missing);
            EvaluateDataRetention(evidence, missing);
        }

        EvaluateBreachNotificationClause(evidence, reasons, citations);

        var level = reasons.Count switch
        {
            0 => RiskLevel.Low,
            1 => RiskLevel.Medium,
            _ => RiskLevel.High,
        };

        return new RiskAssessment(level, reasons, citations, missing);
    }

    private static void EvaluateSoc2(
        IReadOnlyList<Evidence> evidence,
        DateTimeOffset asOf,
        List<RiskReason> reasons,
        List<Citation> citations,
        List<EvidenceType> missing)
    {
        // Vendor-supplied SOC 2 attestations are kept for citation/audit but cannot satisfy the
        // control — only an internally-recorded report counts. This is what defends against a
        // vendor uploading a "SOC 2 report" whose snippet claims compliance.
        var internalSoc2 = evidence
            .Where(e => e.Type == EvidenceType.Soc2Report && e.Source == EvidenceSource.Internal)
            .ToList();

        if (internalSoc2.Count == 0)
        {
            reasons.Add(RiskReason.MissingSoc2Report);
            missing.Add(EvidenceType.Soc2Report);
            return;
        }

        var asOfDate = DateOnly.FromDateTime(asOf.UtcDateTime);
        var recent = internalSoc2
            .Where(e => e.IssuedDate is not null
                        && asOfDate.DayNumber - e.IssuedDate.Value.DayNumber <= Soc2RecencyDays)
            .ToList();

        if (recent.Count == 0)
        {
            reasons.Add(RiskReason.Soc2ReportNotRecent);
        }
        else
        {
            citations.AddRange(recent.Select(ToCitation));
        }
    }

    private static void EvaluateDataRetention(
        IReadOnlyList<Evidence> evidence,
        List<EvidenceType> missing)
    {
        // Informational only — absence adds to missingEvidence but does not block.
        var present = evidence.Any(e =>
            e.Type == EvidenceType.DataRetentionPolicy && e.Source == EvidenceSource.Internal);

        if (!present)
        {
            missing.Add(EvidenceType.DataRetentionPolicy);
        }
    }

    private static void EvaluateBreachNotificationClause(
        IReadOnlyList<Evidence> evidence,
        List<RiskReason> reasons,
        List<Citation> citations)
    {
        var internalContracts = evidence
            .Where(e => e.Type == EvidenceType.Contract && e.Source == EvidenceSource.Internal)
            .ToList();

        var anyLackingClause = internalContracts.Any(c => c.HasBreachNotificationClause != true);
        if (internalContracts.Count == 0 || anyLackingClause)
        {
            reasons.Add(RiskReason.ContractMissingBreachNotificationClause);
        }

        citations.AddRange(internalContracts
            .Where(c => c.HasBreachNotificationClause == true)
            .Select(ToCitation));
    }

    private static Citation ToCitation(Evidence e) => new(e.Id, e.Snippet);
}
