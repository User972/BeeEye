using System.Net;
using BeeEye.Shared.Security;
using BeeEye.Shared.Web.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BeeEye.IntegrationTests;

/// <summary>
/// End-to-end tests for authentication and permission-based authorization (ADR 0008).
/// <para>
/// The base <see cref="IntegrationTestFactory"/> runs in Development, where reads are deliberately
/// relaxed so the SPA works before its sign-in flow ships. These tests re-host the API in the
/// <b>secured</b> posture to prove 401/403/200 behave correctly, and assert that the development auth
/// provider cannot be selected outside Development.
/// </para>
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class SecurityApiTests(IntegrationTestFactory factory)
{
    private const string CockpitUrl = "/api/v1/executive-insights/decision-feed";
    private const string InventoryUrl = "/api/v1/inventory/summary";

    /// <summary>Re-hosts with reads protected and the local provider issuing the given roles.</summary>
    private WebApplicationFactory<Program> SecuredAs(params string[] roles)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.RequireAuthenticatedReads)}", "true");
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Provider)}", nameof(AuthProvider.LocalDev));

            // Replace the default role list rather than appending to it.
            builder.UseSetting("Auth:LocalDevUser:Roles", string.Empty);
            for (var i = 0; i < roles.Length; i++)
            {
                builder.UseSetting($"Auth:LocalDevUser:Roles:{i}", roles[i]);
            }
        });
    }

    /// <summary>Re-hosts with reads protected and Entra bearer auth, so a request without a token is anonymous.</summary>
    private WebApplicationFactory<Program> SecuredWithBearer()
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.RequireAuthenticatedReads)}", "true");
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Provider)}", nameof(AuthProvider.EntraId));
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Authority)}", "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/v2.0");
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Audience)}", "api://beeeye-test");
        });
    }

    // ---------------------------------------------------------------- 401

    [Fact]
    public async Task Anonymous_request_to_a_protected_read_is_rejected_with_401()
    {
        using var secured = SecuredWithBearer();
        var response = await secured.CreateClient().GetAsync(CockpitUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_request_is_rejected_rather_than_returning_an_empty_200()
    {
        // Silently returning an empty body would hide an authorization defect; the threat model
        // requires 401/403, never 200-with-nothing.
        using var secured = SecuredWithBearer();
        var response = await secured.CreateClient().GetAsync(InventoryUrl);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_malformed_bearer_token_is_rejected_with_401()
    {
        using var secured = SecuredWithBearer();
        var client = secured.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "not-a-jwt");

        var response = await client.GetAsync(CockpitUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------------------------------------------------------------- 403

    [Fact]
    public async Task Authenticated_user_without_the_permission_is_rejected_with_403()
    {
        // IT/Admin deliberately holds no executive-cockpit.view.
        using var secured = SecuredAs(PlatformRoles.ItAdmin);
        var response = await secured.CreateClient().GetAsync(CockpitUrl);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_user_with_no_roles_at_all_is_rejected_with_403()
    {
        using var secured = SecuredAs();
        var response = await secured.CreateClient().GetAsync(CockpitUrl);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_unmapped_role_grants_nothing()
    {
        using var secured = SecuredAs("SomeEntraGroupWeDoNotMap");
        var response = await secured.CreateClient().GetAsync(CockpitUrl);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------------------------------------------------------- 200

    [Fact]
    public async Task Authenticated_user_with_the_permission_is_allowed()
    {
        using var secured = SecuredAs(PlatformRoles.Executive);
        var response = await secured.CreateClient().GetAsync(CockpitUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Permission_is_evaluated_per_endpoint_not_per_user()
    {
        // IT/Admin can reach nothing on the cockpit but is a legitimate user; the Analyst can reach
        // inventory. Proves authorization is about the operation, not about being logged in.
        using var asAdmin = SecuredAs(PlatformRoles.ItAdmin);
        using var asAnalyst = SecuredAs(PlatformRoles.Analyst);

        Assert.Equal(HttpStatusCode.Forbidden, (await asAdmin.CreateClient().GetAsync(InventoryUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await asAnalyst.CreateClient().GetAsync(InventoryUrl)).StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/inventory/summary", PlatformRoles.Analyst)]
    [InlineData("/api/v1/forecasting/forecast", PlatformRoles.Analyst)]
    [InlineData("/api/v1/recommendations/order-optimisation", PlatformRoles.Analyst)]
    [InlineData("/api/v1/procurement/recommendations", PlatformRoles.Analyst)]
    [InlineData("/api/v1/sales-actuals/config-demand/summary", PlatformRoles.Analyst)]
    [InlineData("/api/v1/after-sales/service-intensity/summary", PlatformRoles.Analyst)]
    [InlineData("/api/v1/spare-parts/demand/summary", PlatformRoles.Analyst)]
    [InlineData("/api/v1/executive-insights/decision-feed", PlatformRoles.Executive)]
    public async Task Every_protected_module_admits_a_correctly_permissioned_caller(string url, string role)
    {
        using var secured = SecuredAs(role);
        var response = await secured.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/inventory/summary")]
    [InlineData("/api/v1/forecasting/forecast")]
    [InlineData("/api/v1/recommendations/order-optimisation")]
    [InlineData("/api/v1/procurement/recommendations")]
    [InlineData("/api/v1/sales-actuals/config-demand/summary")]
    [InlineData("/api/v1/after-sales/service-intensity/summary")]
    [InlineData("/api/v1/spare-parts/demand/summary")]
    [InlineData("/api/v1/executive-insights/decision-feed")]
    public async Task Every_protected_module_rejects_an_anonymous_caller(string url)
    {
        using var secured = SecuredWithBearer();
        var response = await secured.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------------------------------------------------------------- rollout compatibility

    [Fact]
    public async Task Development_default_keeps_reads_anonymous_so_existing_consumers_still_work()
    {
        // The unmodified factory is the Development posture used by every other test file.
        var response = await factory.CreateClient().GetAsync(CockpitUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_endpoints_stay_reachable_without_authentication()
    {
        // Probes must never require a token, or orchestration cannot determine liveness.
        using var secured = SecuredWithBearer();
        var client = secured.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    // ---------------------------------------------------------------- the third guard

    [Fact]
    public void The_development_auth_provider_cannot_be_selected_outside_development()
    {
        // Guard 3 (ADR 0008 §2.3): the host must abort, not fall back to a fake identity.
        using var production = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Provider)}", nameof(AuthProvider.LocalDev));
        });

        var ex = Assert.Throws<InsecureConfigurationException>(() => production.CreateClient());

        Assert.Contains("LocalDev", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Production", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Entra_without_an_authority_aborts_the_host_rather_than_accepting_any_token()
    {
        using var misconfigured = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Provider)}", nameof(AuthProvider.EntraId));
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Audience)}", "api://beeeye-test");
        });

        var ex = Assert.Throws<InsecureConfigurationException>(() => misconfigured.CreateClient());

        Assert.Contains("Authority", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Entra_without_an_audience_aborts_the_host_rather_than_accepting_another_apps_token()
    {
        using var misconfigured = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Provider)}", nameof(AuthProvider.EntraId));
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Authority)}", "https://login.microsoftonline.com/t/v2.0");
        });

        var ex = Assert.Throws<InsecureConfigurationException>(() => misconfigured.CreateClient());

        Assert.Contains("Audience", ex.Message, StringComparison.Ordinal);
    }
}
