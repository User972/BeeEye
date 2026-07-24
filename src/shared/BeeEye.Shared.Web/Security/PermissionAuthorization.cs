using System.Security.Claims;
using BeeEye.Shared.Security;
using Microsoft.AspNetCore.Authorization;

namespace BeeEye.Shared.Web.Security;

/// <summary>Requires the caller to hold a single fine-grained permission.</summary>
/// <param name="Permission">A value from <see cref="Permissions"/>.</param>
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>
/// Grants a <see cref="PermissionRequirement"/> when the caller's roles map to that permission.
/// <para>
/// The role → permission expansion is the one in <see cref="RolePermissions"/>, so authorization is
/// evaluated against permissions even though the identity provider supplies roles. Nothing here reads
/// a role name directly — that is the point of ADR 0008 §2.2.
/// </para>
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        // An unauthenticated caller is never granted a permission. Returning without succeeding
        // yields 401/403 from the middleware rather than a silent empty 200.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        var roles = context.User.FindAll(ClaimTypes.Role)
            .Concat(context.User.FindAll("roles"))
            .Select(c => c.Value);

        if (RolePermissions.Grants(roles, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Convenience accessors for the authenticated caller.</summary>
public static class PrincipalExtensions
{
    /// <summary>
    /// The stable subject identifier for the caller — Entra's <c>oid</c> where present, otherwise the
    /// standard subject claim. This is the value recorded as <c>decided_by</c> on a decision record
    /// (ADR 0006), so it must be stable across sessions and never a display name.
    /// </summary>
    public static string? SubjectId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.FindFirstValue("oid")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");
    }

    /// <summary>Display name for the caller, for presentation only — never for authorization.</summary>
    public static string? DisplayName(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.FindFirstValue("name") ?? principal.Identity?.Name;
    }

    /// <summary>Every permission the caller holds, expanded from their roles.</summary>
    public static IReadOnlySet<string> Permissions(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity?.IsAuthenticated != true)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var roles = principal.FindAll(ClaimTypes.Role)
            .Concat(principal.FindAll("roles"))
            .Select(c => c.Value);

        return RolePermissions.ForRoles(roles);
    }
}
