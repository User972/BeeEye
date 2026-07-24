using BeeEye.Shared.Modularity;
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
    public string Status => "operational";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // The authentication scheme, policies and role→permission expansion are composition-root
        // concerns registered by AddBeeEyeSecurity (ADR 0008); this module only exposes them.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => IdentityEndpoints.Map(endpoints);
}
