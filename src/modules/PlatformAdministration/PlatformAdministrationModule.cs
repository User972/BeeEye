using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.PlatformAdministration;

/// <summary>Feature flags, licensing entitlements and platform configuration.</summary>
public sealed class PlatformAdministrationModule : IModule
{
    public string Name => "Platform Administration";
    public string RoutePrefix => "platform-admin";
    public string Description => "Feature flags, licensing entitlements and platform configuration.";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scaffolded: Platform Administration application, domain and persistence services register here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, "scaffolded"));
        info.WithName("PlatformAdministration_Info");
        info.WithSummary("Platform Administration module information");
    }
}
