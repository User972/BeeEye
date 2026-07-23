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
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        AfterSalesEndpoints.Map(endpoints);
    }
}
