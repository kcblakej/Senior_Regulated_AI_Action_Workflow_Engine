using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using RegulatedAiWorkflow.Contracts;
using RegulatedAiWorkflow.Data;
using RegulatedAiWorkflow.Workflow;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IEvidenceRepository>(_ => new InMemoryEvidenceRepository(SeedData.Evidence()));
builder.Services.AddSingleton<IPolicyRepository>(_ => new InMemoryPolicyRepository(SeedData.Policies()));
builder.Services.AddSingleton<IVendorRepository>(_ => new InMemoryVendorRepository(SeedData.Vendors()));
builder.Services.AddSingleton<IAuditLog, InMemoryAuditLog>();
builder.Services.AddSingleton<AuditWriter>();
builder.Services.AddSingleton<IActionExecutor, InMemoryActionExecutor>();
builder.Services.AddSingleton<WorkflowOrchestrator>();

var app = builder.Build();

// Structured error envelope. Never leak stack traces — production observability lives in
// logs and traces, not in HTTP responses. Malformed request bodies surface as
// BadHttpRequestException from the model binder; map those to 400 so clients can tell the
// difference between "you sent garbage" and "we broke."
app.UseExceptionHandler(handler => handler.Run(async ctx =>
{
    var feature = ctx.Features.Get<IExceptionHandlerFeature>();
    if (feature?.Error is BadHttpRequestException)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new ErrorResponse("invalid_json", ctx.TraceIdentifier));
        return;
    }
    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsJsonAsync(new ErrorResponse("internal_error", ctx.TraceIdentifier));
}));

// Liveness probe used by the smoke test and during local development.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/workflow", async (WorkflowRequest request, WorkflowOrchestrator orchestrator) =>
{
    // In production, tenantId / userId / role would come from the authenticated principal
    // (OIDC token claims), not from the request body. Accepting them in the body here is a
    // deliberate slice simplification — see PRODUCTION_NOTES for the auth boundary and how
    // header smuggling of tenantId is prevented.
    if (string.IsNullOrWhiteSpace(request.TenantId)
        || string.IsNullOrWhiteSpace(request.UserId)
        || string.IsNullOrWhiteSpace(request.Role)
        || string.IsNullOrWhiteSpace(request.Question))
    {
        return Results.BadRequest(new ErrorResponse("missing_required_field"));
    }
    return Results.Ok(await orchestrator.RunAsync(request));
});

// Demo-only audit read, scoped exactly like evidence: tenantId is enforced inside the
// IAuditLog implementation, not at this edge. Production needs its own RBAC — audit-readers
// are a distinct role from workflow callers. See PRODUCTION_NOTES.
app.MapGet("/audit", (string? tenantId, IAuditLog log) =>
{
    if (string.IsNullOrWhiteSpace(tenantId))
    {
        return Results.BadRequest(new ErrorResponse("missing_tenant_id"));
    }
    return Results.Ok(log.GetByTenant(tenantId));
});

app.Run();

// Exposed so WebApplicationFactory<Program> can be used from the test project.
public partial class Program { }
