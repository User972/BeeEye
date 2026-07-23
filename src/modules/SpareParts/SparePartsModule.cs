using BeeEye.Analytics.Decisions;
using BeeEye.Analytics.Explainability;
using BeeEye.Modules.SpareParts.Application;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.SpareParts;

/// <summary>Intermittent spare-parts demand prediction and stocking recommendations (UC7).</summary>
public sealed class SparePartsModule : IModule
{
    public string Name => "Spare Parts";
    public string RoutePrefix => "spare-parts";
    public string Description => "Intermittent spare-parts demand prediction and stocking ranges (UC7).";
    public string Status => "operational";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<SparePartsReadService>();

        // Contributes this context's material exceptions to the Executive Decision Cockpit (UC8).
        services.AddScoped<IDecisionSignalProvider, SparePartsDecisionSignalProvider>();

        // Answers "why stock this many?" for the global explainability drawer (S3).
        services.AddScoped<IExplainabilityProvider, SparePartsExplainabilityProvider>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        SparePartsEndpoints.Map(endpoints);
    }
}
