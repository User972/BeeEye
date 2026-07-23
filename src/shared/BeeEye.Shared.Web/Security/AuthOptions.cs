namespace BeeEye.Shared.Web.Security;

/// <summary>Which authentication provider the host uses.</summary>
public enum AuthProvider
{
    /// <summary>Microsoft Entra ID — the only provider permitted outside Development.</summary>
    EntraId,

    /// <summary>
    /// Development-only stub that authenticates every request as a configured local developer, so the
    /// stack runs without an Entra tenant. Fenced by three guards — see
    /// <c>docs/adr/0008-authentication-and-authorization.md</c> §2.3.
    /// </summary>
    LocalDev,
}

/// <summary>
/// Authentication and authorization configuration, bound from the <c>Auth</c> section.
/// Defaults are chosen so that an <b>absent</b> configuration is the secure one: Entra ID, and reads
/// requiring authentication.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Provider to use. Defaults to <see cref="AuthProvider.EntraId"/>.</summary>
    public AuthProvider Provider { get; set; } = AuthProvider.EntraId;

    /// <summary>Entra ID tenant authority, e.g. <c>https://login.microsoftonline.com/{tenantId}/v2.0</c>.</summary>
    public string? Authority { get; set; }

    /// <summary>BeeEye's registered API app-id URI; the token's <c>aud</c> must equal this.</summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Whether <b>read</b> endpoints require an authenticated principal. State-changing operations
    /// always require one regardless of this setting (ADR 0008 §2.4). Defaults to <c>true</c>; the
    /// host lowers it only in Development.
    /// </summary>
    public bool RequireAuthenticatedReads { get; set; } = true;

    /// <summary>Clock-skew allowance for <c>exp</c>/<c>nbf</c>. Capped at two minutes.</summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>The identity the development provider issues. Ignored unless <see cref="Provider"/> is LocalDev.</summary>
    public LocalDevUserOptions LocalDevUser { get; set; } = new();
}

/// <summary>The principal the development provider issues.</summary>
public sealed class LocalDevUserOptions
{
    public string SubjectId { get; set; } = "00000000-0000-0000-0000-00000000dev0";

    public string Name { get; set; } = "Local Developer";

    /// <summary>
    /// Roles the local developer holds.
    /// <para>
    /// Deliberately empty here rather than pre-populated: configuration binding <i>appends</i> to an
    /// initialised list, so a default would survive an explicit override and silently grant more than
    /// the developer asked for. The "all roles" convenience default is applied by the host only when
    /// the configuration supplies no <c>Roles</c> section at all — see <c>SecurityRegistration</c>.
    /// </para>
    /// </summary>
    public IList<string> Roles { get; set; } = [];
}
