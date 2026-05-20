using System.ComponentModel.DataAnnotations;

namespace RegulatedAiWorkflow.Contracts;

public sealed record WorkflowRequest(
    [property: Required] string TenantId,
    [property: Required] string UserId,
    [property: Required] string Role,
    [property: Required] string Question,
    string? RequestedAction = null,
    string? ApprovedBy = null);