using System.Security.Claims;
using BeeEye.Shared.Api;
using BeeEye.Shared.Web.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BeeEye.Modules.Identity;

/// <summary>Who the caller is, as the SPA needs to know it.</summary>
/// <param name="IsAuthenticated">False when there is no principal; the arrays are then empty.</param>
/// <param name="SubjectId">
/// The stable subject id recorded as the actor on every decision. Never a display name, and never
/// anyone else's — this endpoint reports only the caller.
/// </param>
/// <param name="DisplayName">For presentation only. Never used for authorization.</param>
/// <param name="Roles">Role claims as the identity provider supplied them.</param>
/// <param name="Permissions">
/// The expanded permissions the caller holds. The SPA uses these to decide which controls to
/// <i>render</i>; the server decides which it will <i>accept</i>, and the two are not the same thing.
/// </param>
public sealed record CurrentUserDto(
    bool IsAuthenticated,
    string? SubjectId,
    string? DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

/// <summary>The caller's own identity — the SPA's entry point into permission-aware rendering.</summary>
internal static class IdentityEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"{ApiRoutes.V1}/identity").WithTags("Identity & Access");

        group.MapGet("/me", (ClaimsPrincipal user) =>
            {
                // Deliberately anonymous. A signed-out SPA must be able to render its signed-out state
                // from a successful response; making this endpoint 401 would mean the very first
                // request of every anonymous session is a failure the client has to special-case.
                if (user.Identity?.IsAuthenticated != true)
                {
                    return Results.Ok(new CurrentUserDto(false, null, null, [], []));
                }

                var roles = user.FindAll(ClaimTypes.Role)
                    .Concat(user.FindAll("roles"))
                    .Select(c => c.Value)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToList();

                return Results.Ok(new CurrentUserDto(
                    true,
                    user.SubjectId(),
                    user.DisplayName(),
                    roles,
                    [.. user.Permissions().Order(StringComparer.Ordinal)]));
            })
            .AllowAnonymous()
            .WithName("Identity_Me")
            .WithSummary("The caller's own identity, roles and permissions")
            .Produces<CurrentUserDto>();
    }
}
