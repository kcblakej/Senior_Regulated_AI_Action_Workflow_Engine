using RegulatedAiWorkflow.Domain;

namespace RegulatedAiWorkflow.Data;

public interface IEvidenceRepository
{
    IReadOnlyList<Evidence> Search(string tenantId, string query);

    Evidence? GetById(string tenantId, string id);
}