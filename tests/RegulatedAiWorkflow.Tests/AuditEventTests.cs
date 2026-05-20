namespace RegulatedAiWorkflow.Tests;

[TestClass]
public class AuditEventTests
{
    [TestMethod]
    public async Task Audit_EveryAttemptWritesAnAuditEvent()
    {
        var fx = TestHarness.Build();

        var blocked = await fx.Orchestrator.RunAsync(new WorkflowRequest(
            TenantId: "tenant-a",
            UserId: "alice@example.com",
            Role: "compliance_officer",
            Question: "Can we approve Vendor X to process customer payment data?",
            RequestedAction: "markVendorApproved",
            ApprovedBy: null));

        var approved = await fx.Orchestrator.RunAsync(new WorkflowRequest(
            TenantId: "tenant-a",
            UserId: "alice@example.com",
            Role: "compliance_officer",
            Question: "Can we approve Vendor X to process customer payment data?",
            RequestedAction: "markVendorApproved",
            ApprovedBy: "bob@example.com"));

        var events = fx.Audit.GetByTenant("tenant-a");

        var blockedEvents = events.Where(e => e.CorrelationId == blocked.CorrelationId).ToList();
        var approvedEvents = events.Where(e => e.CorrelationId == approved.CorrelationId).ToList();

        Assert.IsTrue(blockedEvents.Any(e => e.EventType == AuditEventType.WorkflowStarted));
        Assert.IsTrue(blockedEvents.Any(e => e.EventType == AuditEventType.DecisionComputed));
        Assert.IsTrue(blockedEvents.Any(e => e.EventType == AuditEventType.ActionBlocked));
        Assert.IsTrue(blockedEvents.Any(e => e.EventType == AuditEventType.WorkflowCompleted));
        Assert.IsTrue(blockedEvents.All(e => e.TenantId == "tenant-a"));

        Assert.IsTrue(approvedEvents.Any(e => e.EventType == AuditEventType.WorkflowStarted));
        Assert.IsTrue(approvedEvents.Any(e => e.EventType == AuditEventType.ActionExecuted));
        Assert.IsTrue(approvedEvents.Any(e => e.EventType == AuditEventType.WorkflowCompleted));
        Assert.IsTrue(approvedEvents.All(e => e.TenantId == "tenant-a"));

        // The raw question text must not appear in the audit log. We store its SHA-256 hash instead.
        var expectedHash = AuditWriter.HashInput("Can we approve Vendor X to process customer payment data?");
        Assert.IsTrue(events.All(e => e.InputHash == expectedHash));
        Assert.IsTrue(events.All(e => !string.IsNullOrEmpty(e.CorrelationId)));
    }
}
