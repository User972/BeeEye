using BeeEye.Modules.Procurement.Application;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.Procurement;

/// <summary>Procurement quantity optimisation balancing demand, lead time and cost (UC4).</summary>
public sealed class ProcurementModule : IModule
{
    public string Name => "Procurement";
    public string RoutePrefix => "procurement";
    public string Description => "Procurement quantity optimisation balancing demand, lead time and cost (UC4).";
    public string Status => "operational";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ProcurementReadService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ProcurementEndpoints.Map(endpoints);
    }
}
