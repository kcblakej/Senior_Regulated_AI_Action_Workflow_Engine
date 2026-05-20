using System.Security.Cryptography;
using System.Text;

namespace RegulatedAiWorkflow.Workflow;

public sealed class InMemoryActionExecutor : IActionExecutor
{
    private readonly HashSet<string> _executed = new();
    private readonly object _gate = new();

    public ActionExecutionResult ExecuteMockAction(string tenantId, string action, string targetId)
    {
        // Idempotency key binds the operation to its target so repeat calls collapse to the
        // same logical execution. In production this would be a dedupe row written inside the
        // same transaction as the action's effect — here it just flips an in-memory bool.
        var key = ComputeIdempotencyKey(tenantId, action, targetId);

        lock (_gate)
        {
            _executed.Add(key);
        }

        return new ActionExecutionResult(key, Executed: true);
    }

    private static string ComputeIdempotencyKey(string tenantId, string action, string targetId)
    {
        var raw = $"{tenantId}|{action}|{targetId}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
