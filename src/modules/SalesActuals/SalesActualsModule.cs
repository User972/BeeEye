using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.SalesActuals;

/// <summary>Recorded sales orders, invoices, deliveries and sales facts.</summary>
public sealed class SalesActualsModule : IModule
{
    public string Name => "Sales Actuals";
    public string RoutePrefix => "sales-actuals";
    public string Description => "Recorded sales orders, invoices, deliveries and sales facts.";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scaffolded: Sales Actuals application, domain and persistence services register here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, "scaffolded"));
        info.WithName("SalesActuals_Info");
        info.WithSummary("Sales Actuals module information");
    }
}
