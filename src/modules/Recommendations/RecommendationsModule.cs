using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.Recommendations;

/// <summary>Versioned recommendations, evidence and confidence assessments.</summary>
public sealed class RecommendationsModule : IModule
{
    public string Name => "Recommendations";
    public string RoutePrefix => "recommendations";
    public string Description => "Versioned recommendations, evidence and confidence assessments.";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scaffolded: Recommendations application, domain and persistence services register here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, "scaffolded"));
        info.WithName("Recommendations_Info");
        info.WithSummary("Recommendations module information");
    }
}
