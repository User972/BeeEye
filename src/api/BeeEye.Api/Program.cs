using BeeEye.Api.Composition;
using BeeEye.Api.Infrastructure;
using BeeEye.Shared.Api;
using BeeEye.Shared.Time;
using BeeEye.Shared.Web.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// --- Cross-cutting services ------------------------------------------------
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddBeeEyeDatabase(builder.Configuration);

// Authentication + permission-based authorization (ADR 0008). Throws and aborts boot on an unsafe
// configuration — notably selecting the development auth provider outside Development.
builder.Services.AddBeeEyeSecurity(builder.Configuration, builder.Environment);

// RFC 7807 Problem Details for all error responses, enriched with a correlation id.
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions["correlationId"] =
            ctx.HttpContext.Items[CorrelationIdMiddleware.ItemKey] as string
            ?? ctx.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "BeeEye Platform API";
        document.Info.Version = "v1";
        document.Info.Description =
            "AI decision-intelligence platform for ADMC. Read-only analytics in the initial " +
            "implementation. All bounded contexts are mounted under /api/v1.";
        return Task.CompletedTask;
    });
});

builder.Services.AddHealthChecks();

// Strict CORS: the SPA origin only. Configurable per environment.
const string webCors = "web";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];
builder.Services.AddCors(o => o.AddPolicy(webCors, p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

// --- Compose bounded-context modules --------------------------------------
var modules = ApplicationModules.All;
foreach (var module in modules)
{
    module.RegisterServices(builder.Services, builder.Configuration);
}

var app = builder.Build();

// --- Pipeline --------------------------------------------------------------
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(webCors);

// Order matters: authenticate, then authorize, before any endpoint runs.
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    // Serves the OpenAPI document at /openapi/v1.json (drives the typed web client).
    app.MapOpenApi();
}

// Liveness: process is up (no dependency checks). Readiness: all registered checks.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");

// Platform discovery endpoint — lists every mounted module.
app.MapGet($"{ApiRoutes.V1}/platform/modules", () =>
        modules.Select(m => new ModuleInfo(m.Name, m.RoutePrefix, m.Description, m.Status)))
    .WithTags("Platform")
    .WithName("Platform_Modules")
    .WithSummary("List all mounted bounded-context modules");

// Mount every module's endpoints.
foreach (var module in modules)
{
    module.MapEndpoints(app);
}

await app.InitialiseDatabaseAsync();
app.Run();

/// <summary>Exposed so integration tests can spin up the host with WebApplicationFactory.</summary>
public partial class Program;
