using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BeeEye.Shared.Web.Security;

/// <summary>
/// Development-only authentication that issues a configured local principal, so the stack runs
/// without an Entra tenant.
/// <para>
/// <b>This handler is never registered outside Development.</b> Three independent guards stand in
/// front of it (ADR 0008 §2.3): registration is gated on the environment, selecting it requires an
/// explicit <c>Auth:Provider</c> value, and a start-up assertion aborts the process if it is selected
/// anywhere but Development. The assertion <i>fails the host</i> rather than falling back, so a
/// misconfigured deployment does not run with a fake identity — it does not run.
/// </para>
/// <para>
/// It changes <i>who you are</i>, never <i>whether you are checked</i>: authorization policies are
/// evaluated identically under this handler and under Entra ID.
/// </para>
/// </summary>
public sealed class LocalDevAuthenticationHandler(
    IOptionsMonitor<LocalDevAuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<LocalDevAuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "LocalDev";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var user = Options.User;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.SubjectId),
            new("oid", user.SubjectId),
            new("name", user.Name),
        };

        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>Scheme options carrying the local principal to issue.</summary>
public sealed class LocalDevAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    public LocalDevUserOptions User { get; set; } = new();
}
