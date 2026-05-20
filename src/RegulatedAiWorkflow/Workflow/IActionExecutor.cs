namespace RegulatedAiWorkflow.Workflow;

public sealed record ActionExecutionResult(string IdempotencyKey, bool Executed);

public interface IActionExecutor
{
    ActionExecutionResult ExecuteMockAction(string tenantId, string action, string targetId);
}
