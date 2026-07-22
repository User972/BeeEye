using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.Identity;

/// <summary>Authentication, authorisation, granular permissions and data-scope enforcement.</summary>
public sealed class IdentityModule : IModule
{
    public string Name => "Identity & Access";
    public string RoutePrefix => "identity";
    public string Description => "Authentication, authorisation, granular permissions and data-scope enforcement.";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scaffolded: Identity & Access application, domain and persistence services register here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, "scaffolded"));
        info.WithName("Identity_Info");
        info.WithSummary("Identity & Access module information");
    }
}
