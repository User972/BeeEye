using BeeEye.Shared.Security;
using Microsoft.AspNetCore.Builder;

namespace BeeEye.Shared.Web.Security;

/// <summary>
/// Declares the permission an endpoint requires. Modules use these rather than raw
/// <c>RequireAuthorization</c>, so the permission is stated at the endpoint while the rules for
/// enforcing it stay in exactly one place — the policy registration in
/// <see cref="SecurityRegistration"/>.
/// </summary>
public static class EndpointSecurityExtensions
{
    /// <summary>
    /// Requires <paramref name="permission"/> for a <b>read</b> endpoint.
    /// <para>
    /// Read policies honour <see cref="AuthOptions.RequireAuthenticatedReads"/>, which is off by
    /// default in Development so the existing SPA keeps working before the sign-in flow ships, and on
    /// by default in every deployed environment.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentException">
    /// If <paramref name="permission"/> is state-changing. Those must be declared with
    /// <see cref="RequirePermission{TBuilder}"/>, which no configuration can relax — declaring one as
    /// a read would make it relaxable, so it is rejected at start-up rather than shipped.
    /// </exception>
    public static TBuilder RequireReadPermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        EnsureKnown(permission);

        if (Permissions.IsStateChanging(permission))
        {
            throw new ArgumentException(
                $"'{permission}' is state-changing and must be declared with {nameof(RequirePermission)}, "
                + "which cannot be relaxed by configuration.",
                nameof(permission));
        }

        return builder.RequireAuthorization(permission);
    }

    /// <summary>
    /// Requires <paramref name="permission"/> unconditionally — used for every state-changing
    /// operation. No configuration setting relaxes this, in any environment (ADR 0008 §2.4).
    /// </summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        EnsureKnown(permission);

        return builder.RequireAuthorization(permission);
    }

    private static void EnsureKnown(string permission)
    {
        if (!Permissions.All.Contains(permission))
        {
            throw new ArgumentException(
                $"'{permission}' is not a known permission. Add it to {nameof(Permissions)}.{nameof(Permissions.All)} "
                + "so a policy is registered for it; otherwise the endpoint would authorise nothing.",
                nameof(permission));
        }
    }
}
