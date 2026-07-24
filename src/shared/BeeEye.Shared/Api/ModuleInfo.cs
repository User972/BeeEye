namespace BeeEye.Shared.Api;

/// <summary>
/// Discovery payload returned by every module's root endpoint. Lets the web shell
/// and integration tests confirm a bounded context is mounted and reachable.
/// </summary>
/// <param name="Name">Bounded-context name.</param>
/// <param name="RoutePrefix">URL segment under <see cref="ApiRoutes.V1"/>.</param>
/// <param name="Description">One-line description.</param>
/// <param name="Status">Implementation status of the module (e.g. "scaffolded").</param>
public sealed record ModuleInfo(string Name, string RoutePrefix, string Description, string Status);
