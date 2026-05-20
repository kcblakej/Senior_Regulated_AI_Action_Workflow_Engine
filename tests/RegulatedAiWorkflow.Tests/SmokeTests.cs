using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RegulatedAiWorkflow.Tests;

/// <summary>
/// Smoke tests that prove the scaffold is wired up correctly.
/// Real tests (tenant isolation, approval gate, audit, prompt injection,
/// unauthorized role) live in their own files added in later phases.
/// </summary>
[TestClass]
public class SmokeTests
{
    [TestMethod]
    public async Task Health_Endpoint_Returns_Ok()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
