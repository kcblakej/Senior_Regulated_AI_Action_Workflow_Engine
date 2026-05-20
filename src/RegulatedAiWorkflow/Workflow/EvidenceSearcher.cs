using RegulatedAiWorkflow.Data;
using RegulatedAiWorkflow.Domain;

namespace RegulatedAiWorkflow.Workflow;

public static class EvidenceSearcher
{
    // Delegates to the tenant-scoped repository. Evidence snippets are untrusted natural-language
    // text and are returned as-is; they flow through to citations on the response without ever
    // being parsed or used for control flow downstream. See RiskEvaluator for the decision layer.
    public static IReadOnlyList<Evidence> SearchEvidence(
        IEvidenceRepository repository,
        string tenantId,
        string query)
    {
        return repository.Search(tenantId, query);
    }
}
