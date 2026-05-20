namespace RegulatedAiWorkflow.Domain;

public sealed record AuditEvent(
    Guid Id,
    DateTimeOffset Timestamp,
    string CorrelationId,
    string TenantId,
    string UserId,
    string Role,
    AuditEventType EventType,
    string? Action,
    RiskLevel? RiskLevel,
    IReadOnlyList<string> EvidenceIds,
    string? ApprovedBy,
    string InputHash);