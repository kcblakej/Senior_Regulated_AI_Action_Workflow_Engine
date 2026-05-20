namespace RegulatedAiWorkflow.Domain;

public sealed record Vendor(
    string Id,
    string TenantId,
    string Name,
    bool ProcessesPaymentData);