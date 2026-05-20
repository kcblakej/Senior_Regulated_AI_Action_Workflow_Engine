namespace RegulatedAiWorkflow.Contracts;

public sealed record ErrorResponse(string Error, string? CorrelationId = null);
