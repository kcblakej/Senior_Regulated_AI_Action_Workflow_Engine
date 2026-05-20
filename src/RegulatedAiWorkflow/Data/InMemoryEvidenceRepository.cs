using RegulatedAiWorkflow.Domain;

namespace RegulatedAiWorkflow.Data;

public sealed class InMemoryEvidenceRepository : IEvidenceRepository
{
    private readonly IReadOnlyList<Evidence> _items;

    public InMemoryEvidenceRepository(IEnumerable<Evidence> seed)
    {
        _items = seed.ToList();
    }

    // Tenant scoping enforced inside the method so a forgotten filter at the API edge cannot leak across tenants.
    public IReadOnlyList<Evidence> Search(string tenantId, string query)
    {
        return _items.Where(e => e.TenantId == tenantId).ToList();
    }

    public Evidence? GetById(string tenantId, string id)
    {
        return _items.FirstOrDefault(e => e.TenantId == tenantId && e.Id == id);
    }
}