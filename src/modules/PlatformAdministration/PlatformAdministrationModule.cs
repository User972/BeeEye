using BeeEye.Modules.PlatformAdministration.Application;
using BeeEye.Modules.PlatformAdministration.Contracts;
using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using BeeEye.Shared.Security;
using BeeEye.Shared.Web.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.PlatformAdministration;

/// <summary>Feature flags, licensing entitlements and platform configuration — surfaces the read-only Settings screen (V3-GOV-010).</summary>
public sealed class PlatformAdministrationModule : IModule
{
    public string Name => "Platform Administration";
    public string RoutePrefix => "platform-admin";
    public string Description => "Feature flags, licensing entitlements and platform configuration.";
    public string Status => "operational";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<SettingsReadService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name).RequireReadPermission(Permissions.SettingsView);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, Status));
        info.WithName("PlatformAdministration_Info");
        info.WithSummary("Platform Administration module information");

        group.MapGet("/settings", (SettingsReadService svc) => Results.Ok(svc.Build()))
            .WithName("PlatformAdministration_Settings")
            .WithSummary("The platform's current risk configuration, read-only (V3-GOV-010)")
            .Produces<SettingsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
