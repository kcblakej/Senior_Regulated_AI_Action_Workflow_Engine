using System.Security.Cryptography;
using System.Text;
using RegulatedAiWorkflow.Data;
using RegulatedAiWorkflow.Domain;

namespace RegulatedAiWorkflow.Workflow;

public sealed class AuditWriter
{
    private readonly IAuditLog _log;
    private readonly TimeProvider _time;

    public AuditWriter(IAuditLog log, TimeProvider time)
    {
        _log = log;
        _time = time;
    }

    public void WriteAuditEvent(
        string correlationId,
        AuditEventType eventType,
        string tenantId,
        string userId,
        string role,
        string inputHash,
        string? action = null,
        RiskLevel? riskLevel = null,
        IReadOnlyList<string>? evidenceIds = null,
        string? approvedBy = null)
    {
        var ev = new AuditEvent(
            Id: Guid.NewGuid(),
            Timestamp: _time.GetUtcNow(),
            CorrelationId: correlationId,
            TenantId: tenantId,
            UserId: userId,
            Role: role,
            EventType: eventType,
            Action: action,
            RiskLevel: riskLevel,
            EvidenceIds: evidenceIds ?? Array.Empty<string>(),
            ApprovedBy: approvedBy,
            InputHash: inputHash);
        _log.Append(ev);
    }

    // Hash inputs (e.g. the user's free-form question) instead of storing them raw. Production
    // questions may contain PII or payment details; the audit log should record that an event
    // happened without becoming a secondary store of sensitive prompt content.
    public static string HashInput(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
