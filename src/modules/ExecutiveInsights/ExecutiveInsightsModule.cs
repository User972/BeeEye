using BeeEye.Analytics.Explainability;
using BeeEye.Modules.ExecutiveInsights.Application;
using BeeEye.Modules.ExecutiveInsights.Contracts;
using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using BeeEye.Shared.Security;
using BeeEye.Shared.Time;
using BeeEye.Shared.Web.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.ExecutiveInsights;

/// <summary>Executive decision cockpit aggregating material module exceptions (UC8).</summary>
public sealed class ExecutiveInsightsModule : IModule
{
    public string Name => "Executive Insights";
    public string RoutePrefix => "executive-insights";
    public string Description => "Executive decision cockpit aggregating material module exceptions (UC8).";
    public string Status => "operational";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<DecisionFeedService>();

        // Explains a cockpit decision and the monthly brief itself for the drawer (S3).
        services.AddScoped<IExplainabilityProvider, CockpitExplainabilityProvider>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name).RequireReadPermission(Permissions.ExecutiveCockpitView);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, Status));
        info.WithName("ExecutiveInsights_Info");
        info.WithSummary("Executive Insights module information");

        group.MapGet("/decision-feed", async (DecisionFeedService svc, IClock clock, CancellationToken ct) =>
            {
                var feed = await svc.BuildAsync(clock.UtcNow, ct);
                return Results.Ok(feed);
            })
            .WithName("ExecutiveInsights_DecisionFeed")
            .WithSummary("Ranked cross-module decisions needing attention, with headline aggregates")
            .Produces<DecisionFeedResponse>();
    }
}
