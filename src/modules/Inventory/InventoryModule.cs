using BeeEye.Analytics.Decisions;
using BeeEye.Analytics.Explainability;
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

        // Contributes this context's material exceptions to the Executive Decision Cockpit (UC8).
        services.AddScoped<IDecisionSignalProvider, InventoryDecisionSignalProvider>();

        // Answers "why is this unit at risk?" for the global explainability drawer (S3).
        services.AddScoped<IExplainabilityProvider, InventoryExplainabilityProvider>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        InventoryEndpoints.Map(endpoints);
    }
}
