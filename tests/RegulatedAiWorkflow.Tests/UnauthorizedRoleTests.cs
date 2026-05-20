namespace RegulatedAiWorkflow.Tests;

[TestClass]
public class UnauthorizedRoleTests
{
    [TestMethod]
    public async Task UnauthorizedRole_CannotExecuteEvenWithValidTenant()
    {
        // Build a low-risk scenario deliberately: a non-payment vendor with a compliant contract.
        // SOC 2 / data retention are not required (payment-data gate is off), and the breach
        // clause is present — so the evaluator returns RiskLevel.Low.
        var vendors = new[]
        {
            new Vendor("acme", "tenant-a", "Acme Co", ProcessesPaymentData: false),
        };
        var evidence = new[]
        {
            new Evidence(
                Id: "contract-acme-1",
                TenantId: "tenant-a",
                VendorId: "acme",
                Type: EvidenceType.Contract,
                Source: EvidenceSource.Internal,
                Snippet: "Standard MSA with breach notification clause.",
                IssuedDate: new DateOnly(2025, 1, 1),
                HasBreachNotificationClause: true),
        };

        var fx = TestHarness.Build(evidence: evidence, vendors: vendors);

        var response = await fx.Orchestrator.RunAsync(new WorkflowRequest(
            TenantId: "tenant-a",
            UserId: "viewer-1@example.com",
            Role: "viewer",
            Question: "Can we approve Acme Co?",
            RequestedAction: "markVendorApproved",
            ApprovedBy: null));

        Assert.AreEqual(RiskLevel.Low, response.RiskLevel,
            "scenario should be low risk so the test isolates the role check");
        Assert.AreEqual(ActionStatus.BlockedUnauthorizedRole, response.ActionStatus);
        Assert.AreEqual(0, fx.Executor.Calls.Count,
            "viewer role must not be able to invoke the action even on a low-risk vendor");
    }
}
