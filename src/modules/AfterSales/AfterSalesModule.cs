using BeeEye.Analytics.Decisions;
using BeeEye.Analytics.Explainability;
using BeeEye.Modules.AfterSales.Application;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.AfterSales;

/// <summary>Sales-to-service correlation and service-intensity analysis (UC6).</summary>
public sealed class AfterSalesModule : IModule
{
    public string Name => "After-Sales";
    public string RoutePrefix => "after-sales";
    public string Description => "Sales-to-service correlation and service-intensity analysis (UC6).";
    public string Status => "operational";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AfterSalesReadService>();

        // Contributes this context's material exceptions to the Executive Decision Cockpit (UC8).
        services.AddScoped<IDecisionSignalProvider, AfterSalesDecisionSignalProvider>();

        // Answers "why is this model service-heavy?" for the explainability drawer (S3).
        services.AddScoped<IExplainabilityProvider, AfterSalesExplainabilityProvider>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        AfterSalesEndpoints.Map(endpoints);
    }
}
