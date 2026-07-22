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
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        OrderEndpoints.Map(endpoints);
    }
}
