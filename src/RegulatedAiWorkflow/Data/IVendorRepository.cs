using RegulatedAiWorkflow.Domain;

namespace RegulatedAiWorkflow.Data;

public interface IVendorRepository
{
    IReadOnlyList<Vendor> GetForTenant(string tenantId);

    Vendor? GetById(string tenantId, string id);
}
