namespace RegulatedAiWorkflow.Tests;

internal static class TestHarness
{
    // Fixed point in time used by every test unless overridden. Keeps SOC 2 recency
    // calculations deterministic regardless of when the suite is run.
    public static readonly DateTimeOffset DefaultNow = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    public static Fixture Build(
        IEnumerable<Evidence>? evidence = null,
        IEnumerable<Policy>? policies = null,
        IEnumerable<Vendor>? vendors = null,
        DateTimeOffset? now = null)
    {
        var time = new FixedTimeProvider(now ?? DefaultNow);
        var evidenceRepo = new InMemoryEvidenceRepository(evidence ?? SeedData.Evidence());
        var policyRepo = new InMemoryPolicyRepository(policies ?? SeedData.Policies());
        var vendorRepo = new InMemoryVendorRepository(vendors ?? SeedData.Vendors());
        var auditLog = new InMemoryAuditLog();
        var auditWriter = new AuditWriter(auditLog, time);
        var executor = new SpyActionExecutor();
        var orchestrator = new WorkflowOrchestrator(
            evidenceRepo, policyRepo, vendorRepo, executor, auditWriter, time);
        return new Fixture(orchestrator, auditLog, executor);
    }

    public sealed record Fixture(
        WorkflowOrchestrator Orchestrator,
        IAuditLog Audit,
        SpyActionExecutor Executor);
}

internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;
    public FixedTimeProvider(DateTimeOffset now) => _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
}

internal sealed class SpyActionExecutor : IActionExecutor
{
    public List<(string TenantId, string Action, string TargetId)> Calls { get; } = new();

    public ActionExecutionResult ExecuteMockAction(string tenantId, string action, string targetId)
    {
        Calls.Add((tenantId, action, targetId));
        return new ActionExecutionResult($"spy-key-{Calls.Count}", Executed: true);
    }
}
