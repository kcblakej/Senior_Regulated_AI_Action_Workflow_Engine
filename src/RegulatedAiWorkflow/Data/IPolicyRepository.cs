using RegulatedAiWorkflow.Domain;

namespace RegulatedAiWorkflow.Data;

public interface IPolicyRepository
{
    IReadOnlyList<Policy> GetForTenant(string tenantId);

    Policy? GetById(string tenantId, string id);
}