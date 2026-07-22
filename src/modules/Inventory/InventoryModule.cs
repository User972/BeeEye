using BeeEye.Modules.Inventory.Application;
using BeeEye.Shared.Modularity;
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
    public string Status => "operational";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<InventoryReadService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        InventoryEndpoints.Map(endpoints);
    }
}
