using RegulatedAiWorkflow.Domain;

namespace RegulatedAiWorkflow.Data;

public interface IAuditLog
{
    void Append(AuditEvent auditEvent);

    IReadOnlyList<AuditEvent> GetByTenant(string tenantId);
}