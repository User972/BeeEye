using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.ModelsAndExperiments;

/// <summary>Model registry, versions, experiments and publication lifecycle.</summary>
public sealed class ModelsAndExperimentsModule : IModule
{
    public string Name => "Models and Experiments";
    public string RoutePrefix => "models";
    public string Description => "Model registry, versions, experiments and publication lifecycle.";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scaffolded: Models and Experiments application, domain and persistence services register here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, "scaffolded"));
        info.WithName("ModelsAndExperiments_Info");
        info.WithSummary("Models and Experiments module information");
    }
}
