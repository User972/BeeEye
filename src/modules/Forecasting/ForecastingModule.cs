using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.Forecasting;

/// <summary>Forecast plans, versions, snapshots and accuracy metrics (UC2).</summary>
public sealed class ForecastingModule : IModule
{
    public string Name => "Forecasting";
    public string RoutePrefix => "forecasting";
    public string Description => "Forecast plans, versions, snapshots and accuracy metrics (UC2).";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scaffolded: Forecasting application, domain and persistence services register here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, "scaffolded"));
        info.WithName("Forecasting_Info");
        info.WithSummary("Forecasting module information");
    }
}
