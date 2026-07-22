using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.Procurement;

/// <summary>Suppliers, purchase orders, lead times and procurement quantity optimisation (UC4).</summary>
public sealed class ProcurementModule : IModule
{
    public string Name => "Procurement";
    public string RoutePrefix => "procurement";
    public string Description => "Suppliers, purchase orders, lead times and procurement quantity optimisation (UC4).";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scaffolded: Procurement application, domain and persistence services register here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, "scaffolded"));
        info.WithName("Procurement_Info");
        info.WithSummary("Procurement module information");
    }
}
