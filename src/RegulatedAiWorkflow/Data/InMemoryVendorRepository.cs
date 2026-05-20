using RegulatedAiWorkflow.Domain;

namespace RegulatedAiWorkflow.Data;

public sealed class InMemoryVendorRepository : IVendorRepository
{
    private readonly IReadOnlyList<Vendor> _items;

    public InMemoryVendorRepository(IEnumerable<Vendor> seed)
    {
        _items = seed.ToList();
    }

    public IReadOnlyList<Vendor> GetForTenant(string tenantId)
    {
        return _items.Where(v => v.TenantId == tenantId).ToList();
    }

    public Vendor? GetById(string tenantId, string id)
    {
        return _items.FirstOrDefault(v => v.TenantId == tenantId && v.Id == id);
    }
}
