using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Shared.Modularity;

/// <summary>
/// The contract every bounded-context module implements so the API host can
/// compose it without knowing its internals.
/// <para>
/// This interface is the <b>only</b> coupling the host has to a module: register
/// services and map endpoints. Modules must never reference another module's
/// implementation types — cross-context communication goes through published
/// contracts, application services or domain events. The architecture tests
/// (<c>tests/architecture</c>) enforce this rule.
/// </para>
/// </summary>
public interface IModule
{
    /// <summary>Human-readable bounded-context name, e.g. "Inventory".</summary>
    string Name { get; }

    /// <summary>
    /// URL segment mounted under <see cref="ApiRoutes.V1"/>, e.g. "inventory"
    /// produces routes under <c>/api/v1/inventory</c>.
    /// </summary>
    string RoutePrefix { get; }

    /// <summary>One-line description surfaced by the platform module registry.</summary>
    string Description { get; }

    /// <summary>
    /// Implementation status surfaced by the platform module registry, e.g. "scaffolded" or
    /// "operational". Defaults to "scaffolded"; modules with live endpoints override it.
    /// </summary>
    string Status => "scaffolded";

    /// <summary>Register this module's services into the shared container.</summary>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Map this module's HTTP endpoints. Implementations should mount a
    /// route group at <c>{ApiRoutes.V1}/{RoutePrefix}</c>.</summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
