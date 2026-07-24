using BeeEye.Analytics.Decisions;
using BeeEye.Analytics.Explainability;
using BeeEye.Modules.SalesActuals.Application;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.SalesActuals;

/// <summary>Recorded sales facts and configuration-level demand insights (UC3).</summary>
public sealed class SalesActualsModule : IModule
{
    public string Name => "Sales Actuals";
    public string RoutePrefix => "sales-actuals";
    public string Description => "Recorded sales facts and configuration-level demand insights (UC3).";
    public string Status => "operational";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ConfigurationReadService>();

        // Contributes this context's material exceptions to the Executive Decision Cockpit (UC8).
        services.AddScoped<IDecisionSignalProvider, ConfigurationDecisionSignalProvider>();

        // Answers "why is this configuration classed this way?" for the explainability drawer (S3).
        services.AddScoped<IExplainabilityProvider, ConfigurationExplainabilityProvider>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ConfigurationEndpoints.Map(endpoints);
    }
}
