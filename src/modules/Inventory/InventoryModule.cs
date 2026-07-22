using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.Inventory;

/// <summary>Inventory items, snapshots, aging and overstock-risk scoring (UC5).</summary>
public sealed class InventoryModule : IModule
{
    public string Name => "Inventory";
    public string RoutePrefix => "inventory";
    public string Description => "Inventory items, snapshots, aging and overstock-risk scoring (UC5).";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scaffolded: Inventory application, domain and persistence services register here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, "scaffolded"));
        info.WithName("Inventory_Info");
        info.WithSummary("Inventory module information");
    }
}
