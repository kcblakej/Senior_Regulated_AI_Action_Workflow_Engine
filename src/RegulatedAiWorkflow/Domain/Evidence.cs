namespace RegulatedAiWorkflow.Domain;

public sealed record Evidence(
    string Id,
    string TenantId,
    string VendorId,
    EvidenceType Type,
    EvidenceSource Source,
    string Snippet,
    DateOnly? IssuedDate = null,
    bool? HasBreachNotificationClause = null);