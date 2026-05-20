using RegulatedAiWorkflow.Domain;

namespace RegulatedAiWorkflow.Data;

public sealed class InMemoryAuditLog : IAuditLog
{
    private readonly List<AuditEvent> _events = new();
    private readonly object _gate = new();

    public void Append(AuditEvent auditEvent)
    {
        lock (_gate)
        {
            _events.Add(auditEvent);
        }
    }

    public IReadOnlyList<AuditEvent> GetByTenant(string tenantId)
    {
        lock (_gate)
        {
            return _events.Where(e => e.TenantId == tenantId).ToList();
        }
    }
}