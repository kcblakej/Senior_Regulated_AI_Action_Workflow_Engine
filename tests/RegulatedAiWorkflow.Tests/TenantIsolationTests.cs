namespace RegulatedAiWorkflow.Tests;

[TestClass]
public class TenantIsolationTests
{
    [TestMethod]
    public async Task TenantIsolation_RequestingTenantACannotSeeTenantBEvidence()
    {
        var fx = TestHarness.Build();
        var tenantBEvidenceIds = SeedData.Evidence()
            .Where(e => e.TenantId == "tenant-b")
            .Select(e => e.Id)
            .ToHashSet();

        // A tenant-a request that *names* a tenant-b evidence id in the question. Even with the
        // foreign id in the prompt, the data layer must not surface it: tenant scoping happens
        // inside the repository, not at the API edge.
        var response = await fx.Orchestrator.RunAsync(new WorkflowRequest(
            TenantId: "tenant-a",
            UserId: "alice@example.com",
            Role: "compliance_officer",
            Question: "Tell me about Vendor X and the evidence-b-injection document.",
            RequestedAction: null,
            ApprovedBy: null));

        foreach (var citation in response.Citations)
        {
            Assert.IsFalse(tenantBEvidenceIds.Contains(citation.DocumentId),
                $"citation {citation.DocumentId} belongs to tenant-b and must not appear in a tenant-a response");
        }

        var tenantAEvents = fx.Audit.GetByTenant("tenant-a");
        Assert.IsTrue(tenantAEvents.Count > 0, "tenant-a should have audit events");
        foreach (var ev in tenantAEvents)
        {
            Assert.AreEqual("tenant-a", ev.TenantId);
            foreach (var id in ev.EvidenceIds)
            {
                Assert.IsFalse(tenantBEvidenceIds.Contains(id),
                    $"audit entry referenced foreign tenant-b evidence id {id}");
            }
        }

        // And nothing was written to tenant-b's slice of the audit log.
        Assert.AreEqual(0, fx.Audit.GetByTenant("tenant-b").Count);
    }
}
