using RegulatedAiWorkflow.Domain;

namespace RegulatedAiWorkflow.Data;

public sealed class InMemoryPolicyRepository : IPolicyRepository
{
    private readonly IReadOnlyList<Policy> _items;

    public InMemoryPolicyRepository(IEnumerable<Policy> seed)
    {
        _items = seed.ToList();
    }

    public IReadOnlyList<Policy> GetForTenant(string tenantId)
    {
        return _items.Where(p => p.TenantId == tenantId).ToList();
    }

    public Policy? GetById(string tenantId, string id)
    {
        return _items.FirstOrDefault(p => p.TenantId == tenantId && p.Id == id);
    }
}