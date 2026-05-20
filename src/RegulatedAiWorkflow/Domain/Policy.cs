namespace RegulatedAiWorkflow.Domain;

public sealed record Policy(
    string Id,
    string TenantId,
    string Title,
    string Body);