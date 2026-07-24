using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.MasterData;

/// <summary>Products, vehicle models, variants, configurations, parts and product hierarchy.</summary>
public sealed class MasterDataModule : IModule
{
    public string Name => "Master Data";
    public string RoutePrefix => "master-data";
    public string Description => "Products, vehicle models, variants, configurations, parts and product hierarchy.";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scaffolded: Master Data application, domain and persistence services register here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, "scaffolded"));
        info.WithName("MasterData_Info");
        info.WithSummary("Master Data module information");
    }
}
