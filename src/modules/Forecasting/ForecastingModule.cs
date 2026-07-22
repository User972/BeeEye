using BeeEye.Modules.Forecasting.Application;
using BeeEye.Shared.Modularity;
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
    public string Status => "operational";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ForecastingReadService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ForecastingEndpoints.Map(endpoints);
    }
}
