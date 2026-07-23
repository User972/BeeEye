using BeeEye.Modules.DecisionsAndOutcomes.Application;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.DecisionsAndOutcomes;

/// <summary>Decision workflow, reviews, approvals and outcomes (ADR 0006).</summary>
public sealed class DecisionsAndOutcomesModule : IModule
{
    public string Name => "Decisions and Outcomes";
    public string RoutePrefix => "decisions";
    public string Description => "The governed decision log: who decided, what they changed, and what resulted.";
    public string Status => "operational";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The only writer of recommendation lifecycle state anywhere in the platform (ADR 0006 §6).
        services.AddScoped<RecommendationTransitionService>();
        services.AddScoped<DecisionService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => DecisionEndpoints.Map(endpoints);
}
