using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.Notifications;

/// <summary>Provider-neutral notifications, preferences, deduplication and delivery status.</summary>
public sealed class NotificationsModule : IModule
{
    public string Name => "Notifications";
    public string RoutePrefix => "notifications";
    public string Description => "Provider-neutral notifications, preferences, deduplication and delivery status.";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scaffolded: Notifications application, domain and persistence services register here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, "scaffolded"));
        info.WithName("Notifications_Info");
        info.WithSummary("Notifications module information");
    }
}
