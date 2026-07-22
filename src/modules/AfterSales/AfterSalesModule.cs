using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.AfterSales;

/// <summary>Service events, warranty, recalls and service-intensity analysis (UC6).</summary>
public sealed class AfterSalesModule : IModule
{
    public string Name => "After-Sales";
    public string RoutePrefix => "after-sales";
    public string Description => "Service events, warranty, recalls and service-intensity analysis (UC6).";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scaffolded: After-Sales application, domain and persistence services register here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, "scaffolded"));
        info.WithName("AfterSales_Info");
        info.WithSummary("After-Sales module information");
    }
}
