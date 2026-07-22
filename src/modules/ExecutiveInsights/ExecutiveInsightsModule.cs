using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
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

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scaffolded: Executive Insights application, domain and persistence services register here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, "scaffolded"));
        info.WithName("ExecutiveInsights_Info");
        info.WithSummary("Executive Insights module information");
    }
}
