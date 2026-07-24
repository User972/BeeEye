using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.Integration;

/// <summary>Oracle Fusion anti-corruption layer, source adapters and ingestion runs.</summary>
public sealed class IntegrationModule : IModule
{
    public string Name => "Data Integration";
    public string RoutePrefix => "integration";
    public string Description => "Oracle Fusion anti-corruption layer, source adapters and ingestion runs.";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scaffolded: Data Integration application, domain and persistence services register here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, "scaffolded"));
        info.WithName("Integration_Info");
        info.WithSummary("Data Integration module information");
    }
}
