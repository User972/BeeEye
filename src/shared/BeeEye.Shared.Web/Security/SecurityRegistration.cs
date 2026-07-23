using BeeEye.Shared.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace BeeEye.Shared.Web.Security;

/// <summary>
/// Thrown at start-up when the security configuration is unsafe. Aborting the host is deliberate:
/// a misconfigured deployment must not run in a degraded security posture.
/// </summary>
public sealed class InsecureConfigurationException(string message) : InvalidOperationException(message);

/// <summary>Composition-root wiring for authentication and permission-based authorization (ADR 0008).</summary>
public static class SecurityRegistration
{
    /// <summary>Signing algorithms accepted from the identity provider — asymmetric only, so
    /// <c>alg: none</c> and symmetric downgrades are rejected outright.</summary>
    private static readonly string[] AllowedAlgorithms =
    [
        SecurityAlgorithms.RsaSha256, SecurityAlgorithms.RsaSha384, SecurityAlgorithms.RsaSha512,
        SecurityAlgorithms.RsaSsaPssSha256, SecurityAlgorithms.RsaSsaPssSha384, SecurityAlgorithms.RsaSsaPssSha512,
        SecurityAlgorithms.EcdsaSha256, SecurityAlgorithms.EcdsaSha384, SecurityAlgorithms.EcdsaSha512,
    ];

    /// <summary>The maximum clock skew tolerated on <c>exp</c>/<c>nbf</c> (threat model §2.2).</summary>
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Registers authentication and one authorization policy per permission in
    /// <see cref="Permissions.All"/>.
    /// </summary>
    /// <exception cref="InsecureConfigurationException">
    /// If the development provider is selected outside Development, or Entra ID is selected without an
    /// authority and audience. Both abort the host rather than degrade.
    /// </exception>
    public static IServiceCollection AddBeeEyeSecurity(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var options = ReadOptions(configuration, environment);
        Validate(options, environment);

        services.AddSingleton(options);
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        // A deployed host running with reads open is a legitimate rollout posture (ADR 0008 §2.4) but
        // never a quiet one — it must be visible in the logs of the environment it is running in.
        if (!options.RequireAuthenticatedReads && !environment.IsDevelopment())
        {
            services.AddHostedService(provider => new RelaxedReadPostureAnnouncer(
                provider.GetRequiredService<ILogger<RelaxedReadPostureAnnouncer>>(), environment));
        }

        if (options.Provider == AuthProvider.LocalDev)
        {
            // Guard 1: only ever reached in Development — Validate() has already thrown otherwise.
            services
                .AddAuthentication(LocalDevAuthenticationHandler.SchemeName)
                .AddScheme<LocalDevAuthenticationSchemeOptions, LocalDevAuthenticationHandler>(
                    LocalDevAuthenticationHandler.SchemeName,
                    schemeOptions => schemeOptions.User = options.LocalDevUser);
        }
        else
        {
            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(jwt =>
                {
                    jwt.Authority = options.Authority;
                    jwt.Audience = options.Audience;
                    jwt.RequireHttpsMetadata = true;
                    jwt.MapInboundClaims = false;

                    jwt.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = options.Authority,
                        ValidateAudience = true,
                        ValidAudience = options.Audience,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidAlgorithms = AllowedAlgorithms,
                        // Capped regardless of configuration: a generous skew is a replay window.
                        ClockSkew = options.ClockSkew > MaxClockSkew ? MaxClockSkew : options.ClockSkew,
                        RoleClaimType = "roles",
                        NameClaimType = "name",
                    };
                });
        }

        services.AddAuthorizationBuilder().AddPermissionPolicies(options);

        return services;
    }

    /// <summary>
    /// Registers one policy per permission, named after the permission itself.
    /// <para>
    /// This is the <b>single</b> place the read-rollout rule is implemented. A read permission becomes
    /// a no-op policy while <see cref="AuthOptions.RequireAuthenticatedReads"/> is off; a
    /// state-changing permission is always enforced, so no configuration can open a write path.
    /// </para>
    /// </summary>
    private static AuthorizationBuilder AddPermissionPolicies(this AuthorizationBuilder builder, AuthOptions options)
    {
        foreach (var permission in Permissions.All)
        {
            var relaxed = !options.RequireAuthenticatedReads && !Permissions.IsStateChanging(permission);

            builder.AddPolicy(permission, policy =>
            {
                if (relaxed)
                {
                    // Rollout posture: reads stay open until the SPA sign-in flow ships. Never
                    // reachable for a state-changing permission, and never the deployed default.
                    policy.RequireAssertion(_ => true);
                    return;
                }

                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(permission));
            });
        }

        return builder;
    }

    private static AuthOptions ReadOptions(IConfiguration configuration, IHostEnvironment environment)
    {
        var section = configuration.GetSection(AuthOptions.SectionName);

        var options = new AuthOptions();
        section.Bind(options);

        var development = environment.IsDevelopment();

        // Development defaults to the local provider so the stack runs with no Entra tenant. The
        // defaults on AuthOptions stay secure (EntraId), so an unconfigured *deployed* host still
        // demands real tokens — the relaxation is opt-out by environment, never by omission.
        if (development && section[nameof(AuthOptions.Provider)] is null)
        {
            options.Provider = AuthProvider.LocalDev;
        }

        // Reads stay open in Development so the SPA works before the sign-in flow ships. Deployed
        // environments keep the secure default of true. State-changing permissions are never affected.
        if (development && section[nameof(AuthOptions.RequireAuthenticatedReads)] is null)
        {
            options.RequireAuthenticatedReads = false;
        }

        // Convenience only: when no Roles section is supplied at all, the local developer gets every
        // platform role so the whole application is reachable. An explicitly supplied section — even an
        // empty one — is honoured exactly, so a developer can reproduce a narrower persona.
        if (options.Provider == AuthProvider.LocalDev
            && !section.GetSection($"{nameof(AuthOptions.LocalDevUser)}:{nameof(LocalDevUserOptions.Roles)}").Exists())
        {
            options.LocalDevUser.Roles = [.. PlatformRoles.All];
        }

        return options;
    }

    /// <summary>
    /// Guard 3 — the start-up assertion. Throws rather than falling back, so an unsafe configuration
    /// cannot boot.
    /// </summary>
    private static void Validate(AuthOptions options, IHostEnvironment environment)
    {
        if (options.Provider == AuthProvider.LocalDev && !environment.IsDevelopment())
        {
            throw new InsecureConfigurationException(
                $"Auth:Provider is '{nameof(AuthProvider.LocalDev)}' in the '{environment.EnvironmentName}' "
                + "environment. The development authentication provider is permitted only in Development. "
                + $"Set Auth:Provider to '{nameof(AuthProvider.EntraId)}'.");
        }

        if (options.Provider != AuthProvider.EntraId)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.Authority))
        {
            throw new InsecureConfigurationException(
                "Auth:Authority is required when Auth:Provider is 'EntraId'. Without an authority no "
                + "token signature can be validated.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InsecureConfigurationException(
                "Auth:Audience is required when Auth:Provider is 'EntraId'. Without an audience a token "
                + "issued for another application would be accepted.");
        }
    }
}

/// <summary>
/// Logs a warning at start-up when a deployed host is serving reads anonymously.
/// <para>
/// This posture is permitted while the SPA sign-in flow is outstanding, so it is not a start-up
/// failure like the guards in <see cref="SecurityRegistration"/>. But a temporary flag that relaxes
/// authorization and leaves no trace is exactly how a temporary flag becomes permanent — ADR 0008
/// names that as a risk — so every deployed boot in this posture says so out loud.
/// </para>
/// </summary>
internal sealed class RelaxedReadPostureAnnouncer(
    ILogger<RelaxedReadPostureAnnouncer> logger, IHostEnvironment environment) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Auth:RequireAuthenticatedReads is false in the '{Environment}' environment: read endpoints "
            + "are served anonymously. This is a rollout-only posture pending the SPA sign-in flow; set it "
            + "to true once that ships. State-changing operations remain enforced.",
            environment.EnvironmentName);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
