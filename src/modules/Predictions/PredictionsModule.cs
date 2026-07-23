using BeeEye.Modules.Predictions.Application;
using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.Predictions;

/// <summary>Model runs, predictions and prediction explanations.</summary>
public sealed class PredictionsModule : IModule
{
    public string Name => "Predictions";
    public string RoutePrefix => "predictions";
    public string Description => "Model runs, predictions and prediction explanations.";

    /// <summary>
    /// Operational as of S3: the global explainability drawer is served from here. Model runs and
    /// prediction storage remain scaffolded.
    /// </summary>
    public string Status => "operational";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Aggregates every context's IExplainabilityProvider. Scoped, because the providers it
        // resolves share the request's DbContext.
        services.AddScoped<ExplainabilityService>();

        // The append-only "Was this useful?" record (V3-DS-006).
        services.AddScoped<ExplainabilityFeedbackService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Boot fails here rather than at request time if two providers claim one subject kind.
        ExplainabilityEndpoints.AssertProvidersAreWellFormed(endpoints);

        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, Status));
        info.WithName("Predictions_Info");
        info.WithSummary("Predictions module information");

        ExplainabilityEndpoints.Map(group);
    }
}
