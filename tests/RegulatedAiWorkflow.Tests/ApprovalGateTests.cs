namespace RegulatedAiWorkflow.Tests;

[TestClass]
public class ApprovalGateTests
{
    [TestMethod]
    public async Task ApprovalGate_HighRiskActionIsBlockedWithoutApproval()
    {
        var fx = TestHarness.Build();

        var response = await fx.Orchestrator.RunAsync(new WorkflowRequest(
            TenantId: "tenant-a",
            UserId: "alice@example.com",
            Role: "compliance_officer",
            Question: "Can we approve Vendor X to process customer payment data?",
            RequestedAction: "markVendorApproved",
            ApprovedBy: null));

        Assert.AreEqual(RiskLevel.High, response.RiskLevel);
        Assert.AreEqual(ActionStatus.BlockedPendingApproval, response.ActionStatus);
        Assert.IsTrue(response.RequiresApproval);
        Assert.AreEqual(0, fx.Executor.Calls.Count,
            "the action executor must not be invoked when an approval is required and missing");
    }

    [TestMethod]
    public async Task ApprovalGate_HighRiskActionIsBlockedWhenApproverEqualsRequester()
    {
        // Four-eyes: an approver that equals the requester does not satisfy the gate.
        var fx = TestHarness.Build();

        var response = await fx.Orchestrator.RunAsync(new WorkflowRequest(
            TenantId: "tenant-a",
            UserId: "alice@example.com",
            Role: "compliance_officer",
            Question: "Can we approve Vendor X to process customer payment data?",
            RequestedAction: "markVendorApproved",
            ApprovedBy: "alice@example.com"));

        Assert.AreEqual(ActionStatus.BlockedPendingApproval, response.ActionStatus);
        Assert.AreEqual(0, fx.Executor.Calls.Count);
    }
}
