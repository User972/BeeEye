using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.Organisation;

/// <summary>Tenant, legal-entity, business-unit, region, branch and location hierarchy.</summary>
public sealed class OrganisationModule : IModule
{
    public string Name => "Organisation";
    public string RoutePrefix => "organisation";
    public string Description => "Tenant, legal-entity, business-unit, region, branch and location hierarchy.";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scaffolded: Organisation application, domain and persistence services register here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, "scaffolded"));
        info.WithName("Organisation_Info");
        info.WithSummary("Organisation module information");
    }
}
