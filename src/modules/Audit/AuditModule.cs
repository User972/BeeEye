using BeeEye.Shared.Api;
using BeeEye.Shared.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.Audit;

/// <summary>Append-only business audit trail.</summary>
public sealed class AuditModule : IModule
{
    public string Name => "Audit";
    public string RoutePrefix => "audit";
    public string Description => "Append-only business audit trail.";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scaffolded: Audit application, domain and persistence services register here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"{ApiRoutes.V1}/{RoutePrefix}");
        group.WithTags(Name);

        var info = group.MapGet("/", () => new ModuleInfo(Name, RoutePrefix, Description, "scaffolded"));
        info.WithName("Audit_Info");
        info.WithSummary("Audit module information");
    }
}
