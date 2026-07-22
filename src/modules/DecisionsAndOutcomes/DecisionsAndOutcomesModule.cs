using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.DecisionsAndOutcomes;

/// <summary>Decision workflow, reviews, assignments, comments and outcomes.</summary>
public sealed class DecisionsAndOutcomesModule : IModule
{
    public string Name => "Decisions and Outcomes";
    public string RoutePrefix => "decisions";
    public string Description => "Decision workflow, reviews, assignments, comments and outcomes.";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scaffolded: Decisions and Outcomes application, domain and persistence services register here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, "scaffolded"));
        info.WithName("DecisionsAndOutcomes_Info");
        info.WithSummary("Decisions and Outcomes module information");
    }
}
