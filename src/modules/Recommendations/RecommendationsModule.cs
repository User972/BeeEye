using BeeEye.Analytics.Decisions;
using BeeEye.Analytics.Explainability;
using BeeEye.Modules.Recommendations.Application;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.Recommendations;

/// <summary>Order-optimisation recommendations balancing demand and constraints (UC1).</summary>
public sealed class RecommendationsModule : IModule
{
    public string Name => "Recommendations";
    public string RoutePrefix => "recommendations";
    public string Description => "Order-optimisation recommendations balancing demand and constraints (UC1).";
    public string Status => "operational";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<OrderReadService>();

        // Contributes this context's material exceptions to the Executive Decision Cockpit (UC8).
        services.AddScoped<IDecisionSignalProvider, OrderDecisionSignalProvider>();

        // Answers "why this order quantity?" for the global explainability drawer (S3).
        services.AddScoped<IExplainabilityProvider, OrderExplainabilityProvider>();

        // Persists engine recommendations as frozen, append-only records (ADR 0006).
        services.AddScoped<RecommendationRecordService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        OrderEndpoints.Map(endpoints);
        RecommendationRecordEndpoints.Map(endpoints);
    }
}
