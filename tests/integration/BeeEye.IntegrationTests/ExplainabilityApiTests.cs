using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BeeEye.Analytics.Explainability;
using BeeEye.Persistence;
using BeeEye.Shared.Idempotency;
using BeeEye.Shared.Security;
using BeeEye.Shared.Web.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeeEye.IntegrationTests;

/// <summary>
/// End-to-end tests for the global explainability drawer's API (S3, V3-DS-006).
/// <para>
/// The properties these exist to prove: every live context can explain its own output, a failing
/// provider degrades to a reported gap rather than a 500 or a silent blank, no exception detail
/// reaches the browser, money survives as an invariantly-parsable string, feedback is attributed,
/// idempotent and append-only, and <b>there is no delete path</b>.
/// </para>
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ExplainabilityApiTests(IntegrationTestFactory factory)
{
    private const string ExplainUrl = "/api/v1/predictions/explain";
    private const string FeedbackUrl = "/api/v1/predictions/explain/feedback";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ---------------------------------------------------------------- hosting helpers

    /// <summary>
    /// Re-hosts with authorization enforced and the caller holding the given roles, plus a distinct
    /// subject id — feedback is attributed to a person, so the tests need more than one.
    /// </summary>
    private WebApplicationFactory<Program> As(string subjectId, params string[] roles) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.RequireAuthenticatedReads)}", "true");
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Provider)}", nameof(AuthProvider.LocalDev));
            builder.UseSetting("Auth:LocalDevUser:SubjectId", subjectId);
            builder.UseSetting("Auth:LocalDevUser:Roles", string.Empty);
            for (var i = 0; i < roles.Length; i++)
            {
                builder.UseSetting($"Auth:LocalDevUser:Roles:{i}", roles[i]);
            }
        });

    private WebApplicationFactory<Program> Executive(string subjectId = "exec-explain") =>
        As(subjectId, PlatformRoles.Executive);

    private WebApplicationFactory<Program> Analyst(string subjectId = "analyst-explain") =>
        As(subjectId, PlatformRoles.Analyst);

    private static HttpRequestMessage Post(string url, object? body, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);

        if (key is not null)
        {
            request.Headers.TryAddWithoutValidation(IdempotencyKey.HeaderName, key);
        }

        if (body is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body, Json), Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
        }

        return request;
    }

    private static string NewKey() => Guid.NewGuid().ToString();

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<string> DetailOf(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        return problem.TryGetProperty("detail", out var detail) ? detail.GetString() ?? string.Empty : string.Empty;
    }

    private static string Query(string kind, string subjectRef) =>
        $"{ExplainUrl}?kind={Uri.EscapeDataString(kind)}&ref={Uri.EscapeDataString(subjectRef)}";

    private static async Task<JsonElement> ExplainAsync(HttpClient client, string kind, string subjectRef) =>
        await ReadAsync(await client.GetAsync(Query(kind, subjectRef)));

    // ---------------------------------------------------------------- subject discovery
    //
    // Subjects are discovered from the same endpoints the screens use rather than hard-coded, so the
    // tests assert "whatever this screen shows can be explained" — not "this one seeded row can be".
    // They also never depend on *how many* records a generation run produces, which is the trap S6
    // recorded.

    private static async Task<string?> FirstAsync(
        HttpClient client, string url, Func<JsonElement, string> project, string arrayProperty = "items")
    {
        var payload = await ReadAsync(await client.GetAsync(url));
        var array = arrayProperty.Length == 0 ? payload : payload.GetProperty(arrayProperty);

        // Paged endpoints nest their rows one level deeper.
        if (array.ValueKind == JsonValueKind.Object && array.TryGetProperty("items", out var nested))
        {
            array = nested;
        }

        var first = array.EnumerateArray().FirstOrDefault();
        return first.ValueKind == JsonValueKind.Undefined ? null : project(first);
    }

    // ---------------------------------------------------------------- one test per subject kind

    [Fact]
    public async Task An_inventory_unit_explains_itself_completely()
    {
        using var executive = Executive("exec-inv");
        var client = executive.CreateClient();

        var stockId = await FirstAsync(client, "/api/v1/inventory/items?pageSize=1", i => i.GetProperty("stockId").GetString()!);
        Assert.NotNull(stockId);

        var payload = await ExplainAsync(client, "inventory-unit", stockId);
        var explanation = AssertComplete(payload, expectedModule: "Inventory Intelligence");

        // UC5's drivers are the model's own additive factor breakdown, and the evidence series is the
        // same arithmetic charted — the richest of the eight providers.
        Assert.NotEmpty(explanation.GetProperty("drivers").EnumerateArray());
        Assert.NotEqual(JsonValueKind.Null, explanation.GetProperty("evidence").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, explanation.GetProperty("ownership").ValueKind);
        Assert.False(explanation.GetProperty("isDemoData").GetBoolean());
    }

    [Fact]
    public async Task An_order_configuration_explains_itself_and_omits_the_evidence_chart()
    {
        using var executive = Executive("exec-order");
        var client = executive.CreateClient();

        var row = await FirstAsync(
            client,
            "/api/v1/recommendations/order-optimisation",
            r => $"{r.GetProperty("model").GetString()}|{r.GetProperty("variant").GetString()}");
        Assert.NotNull(row);

        var payload = await ExplainAsync(client, "order-configuration", row);
        var explanation = AssertComplete(payload, expectedModule: "Order Optimisation");

        // The UC1 screen has no per-configuration chart, so the section is *absent* rather than a
        // placeholder. Asserting the null is what stops someone helpfully filling it later.
        Assert.Equal(JsonValueKind.Null, explanation.GetProperty("evidence").ValueKind);
        Assert.NotNull(explanation.GetProperty("recommendation").GetString());
    }

    [Fact]
    public async Task A_forecast_scope_explains_itself_with_a_back_test_series()
    {
        using var executive = Executive("exec-forecast");
        var client = executive.CreateClient();

        // "|" is the unfiltered scope — the total business, which is what the screen shows first.
        var payload = await ExplainAsync(client, "forecast-scope", "|");
        var explanation = AssertComplete(payload, expectedModule: "Forecast Accuracy");

        Assert.Equal("forecast", explanation.GetProperty("label").GetString());

        var evidence = explanation.GetProperty("evidence");
        Assert.NotEqual(JsonValueKind.Null, evidence.ValueKind);
        Assert.NotEmpty(evidence.GetProperty("points").EnumerateArray());

        // Actual beside fitted: every back-test point carries both series.
        foreach (var point in evidence.GetProperty("points").EnumerateArray())
        {
            Assert.NotEqual(JsonValueKind.Null, point.GetProperty("comparison").ValueKind);
        }

        // A forecast is not a decision, so there is nobody to assign and no workflow footer.
        Assert.Equal(JsonValueKind.Null, explanation.GetProperty("ownership").ValueKind);
    }

    [Fact]
    public async Task A_configuration_explains_itself_as_an_observation_not_a_recommendation()
    {
        using var executive = Executive("exec-config");
        var client = executive.CreateClient();

        var config = await FirstAsync(
            client,
            "/api/v1/sales-actuals/config-demand/configs?pageSize=1",
            c => string.Join('|',
                c.GetProperty("model").GetString(),
                c.GetProperty("variant").GetString(),
                c.GetProperty("colour").GetString(),
                c.GetProperty("interior").GetString()));
        Assert.NotNull(config);

        var payload = await ExplainAsync(client, "configuration", config);
        var explanation = AssertComplete(payload, expectedModule: "Configuration Insights");

        // UC3 observes; it does not advise. The section is omitted rather than filled with a sentence
        // the engine never wrote.
        Assert.Equal(JsonValueKind.Null, explanation.GetProperty("recommendation").ValueKind);
        Assert.Contains(
            explanation.GetProperty("label").GetString(),
            new[] { "calculated", "low", "dq" });
    }

    [Fact]
    public async Task A_procurement_item_explains_itself_and_discloses_the_missing_supplier_feed()
    {
        using var executive = Executive("exec-proc");
        var client = executive.CreateClient();

        var row = await FirstAsync(
            client,
            "/api/v1/procurement/recommendations",
            r => $"{r.GetProperty("model").GetString()}|{r.GetProperty("variant").GetString()}");
        Assert.NotNull(row);

        var payload = await ExplainAsync(client, "procurement-item", row);
        var explanation = AssertComplete(payload, expectedModule: "Procurement Optimisation");

        // V3-CONFLICT-9: no supplier, purchase-order or delivery-performance data exists. The drawer
        // has to say so rather than let a reader assume the safety stock accounts for supplier
        // reliability.
        var assumptions = explanation.GetProperty("assumptions").EnumerateArray()
            .Select(a => a.GetString() ?? string.Empty).ToList();
        Assert.Contains(assumptions, a => a.Contains("supplier", StringComparison.OrdinalIgnoreCase));

        var lineage = explanation.GetProperty("lineage").EnumerateArray()
            .Select(l => l.GetProperty("label").GetString() ?? string.Empty).ToList();
        Assert.Contains(lineage, l => l.Contains("not integrated", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_service_model_explains_itself_as_demo_data_and_states_the_labour_rate()
    {
        using var executive = Executive("exec-svc");
        var client = executive.CreateClient();

        var model = await FirstAsync(
            client,
            "/api/v1/after-sales/service-intensity/by-model?pageSize=1",
            m => m.GetProperty("model").GetString()!,
            arrayProperty: "page");
        Assert.NotNull(model);

        var payload = await ExplainAsync(client, "service-model", model);
        var explanation = AssertComplete(payload, expectedModule: "Sales ↔ Service Correlation");

        Assert.True(explanation.GetProperty("isDemoData").GetBoolean());
        Assert.Contains(
            explanation.GetProperty("lineage").EnumerateArray(),
            l => l.GetProperty("kind").GetString() == "demo");

        // The SAR 350/hour rate S2 recorded in an evidence string is an *assumption*, and it belongs
        // where a reader looks for assumptions.
        var assumptions = explanation.GetProperty("assumptions").EnumerateArray()
            .Select(a => a.GetString() ?? string.Empty).ToList();
        Assert.Contains(assumptions, a => a.Contains("350", StringComparison.Ordinal));
        Assert.Contains(assumptions, a => a.Contains("synthetic demo data", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_part_explains_itself_as_demo_data_with_a_usage_series()
    {
        using var executive = Executive("exec-part");
        var client = executive.CreateClient();

        var partNumber = await FirstAsync(
            client,
            "/api/v1/spare-parts/demand/parts?pageSize=1",
            p => p.GetProperty("partNumber").GetString()!,
            arrayProperty: "page");
        Assert.NotNull(partNumber);

        var payload = await ExplainAsync(client, "part", partNumber);
        var explanation = AssertComplete(payload, expectedModule: "Spare Parts Prediction");

        Assert.True(explanation.GetProperty("isDemoData").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, explanation.GetProperty("evidence").ValueKind);

        // The intermittent-demand method is the interesting thing to explain, so it leads the drivers.
        var drivers = explanation.GetProperty("drivers").EnumerateArray()
            .Select(d => d.GetProperty("label").GetString() ?? string.Empty).ToList();
        Assert.Contains(drivers, d => d.StartsWith("Demand class", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_cockpit_decision_explains_itself_and_carries_ownership()
    {
        using var executive = Executive("exec-cockpit");
        var client = executive.CreateClient();

        var decisionId = await FirstAsync(
            client, "/api/v1/executive-insights/decision-feed", d => d.GetProperty("id").GetString()!, "decisions");
        Assert.NotNull(decisionId);

        var payload = await ExplainAsync(client, "decision", decisionId);
        var explanation = AssertComplete(payload, expectedModule: null);

        // Ownership is what makes the drawer render its workflow footer, and a cockpit decision is
        // exactly the kind of subject that has one.
        var ownership = explanation.GetProperty("ownership");
        Assert.NotEqual(JsonValueKind.Null, ownership.ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(ownership.GetProperty("ownerRole").GetString()));

        // The four priority factors, as the model ranks them.
        Assert.Equal(4, explanation.GetProperty("drivers").EnumerateArray().Count());
    }

    [Fact]
    public async Task The_monthly_brief_explains_how_it_was_generated()
    {
        using var executive = Executive("exec-brief");
        var client = executive.CreateClient();

        var payload = await ExplainAsync(client, "brief", "current");
        var explanation = AssertComplete(payload, expectedModule: "Decision Cockpit");

        Assert.Equal("calculated", explanation.GetProperty("label").GetString());
        Assert.Equal("How this monthly brief was generated", explanation.GetProperty("title").GetString());

        // v3's ckExplainSummary lists the priority model and the contributing modules.
        var drivers = explanation.GetProperty("drivers").EnumerateArray()
            .Select(d => d.GetProperty("label").GetString() ?? string.Empty).ToList();
        Assert.Contains(drivers, d => d.Contains("priority score", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The invariants every explanation must satisfy, whichever context produced it.
    /// </summary>
    private static JsonElement AssertComplete(JsonElement payload, string? expectedModule)
    {
        Assert.Empty(payload.GetProperty("gaps").EnumerateArray());

        var explanation = payload.GetProperty("explanation");
        Assert.NotEqual(JsonValueKind.Null, explanation.ValueKind);

        Assert.False(string.IsNullOrWhiteSpace(explanation.GetProperty("title").GetString()));

        if (expectedModule is not null)
        {
            Assert.Equal(expectedModule, explanation.GetProperty("module").GetString());
        }

        // The label is required and must be one of the eight from engine2.js's LABELS table.
        var label = explanation.GetProperty("label").GetString();
        Assert.Contains(label, ExplanationVocabulary.AllLabelKeys);

        // Lineage is never empty: a figure whose provenance is unstated is the thing this panel exists
        // to prevent.
        var lineage = explanation.GetProperty("lineage").EnumerateArray().ToList();
        Assert.NotEmpty(lineage);
        foreach (var node in lineage)
        {
            Assert.Contains(node.GetProperty("kind").GetString(), ExplanationVocabulary.AllLineageKeys);
        }

        // Every impact tile is pre-formatted invariantly, and its tone is a key rather than a colour.
        foreach (var tile in explanation.GetProperty("impacts").EnumerateArray())
        {
            var value = tile.GetProperty("value").GetString() ?? string.Empty;
            Assert.False(string.IsNullOrWhiteSpace(value));
            Assert.DoesNotContain("var(--", value, StringComparison.Ordinal);

            var tone = tile.GetProperty("tone").GetString();
            Assert.Contains(tone, new[] { "neutral", "positive", "negative", "warning" });
            Assert.DoesNotContain("var(--", tone ?? string.Empty, StringComparison.Ordinal);

            AssertMoneyParsesInvariantly(value);
        }

        return explanation;
    }

    /// <summary>
    /// A SAR figure must survive as something an invariant parse can read back — never
    /// <c>"SAR 1.234,57"</c>, whatever culture the server happens to boot under.
    /// </summary>
    private static void AssertMoneyParsesInvariantly(string value)
    {
        if (!value.StartsWith("SAR ", StringComparison.Ordinal))
        {
            return;
        }

        var body = value["SAR ".Length..].TrimEnd('B', 'M', 'K');

        Assert.True(
            decimal.TryParse(body, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            $"'{value}' is not an invariantly-parsable money string.");
    }

    // ---------------------------------------------------------------- request validation

    [Fact]
    public async Task An_unknown_kind_is_refused_with_the_kinds_that_are_registered()
    {
        using var executive = Executive("exec-unknown-kind");
        var response = await executive.CreateClient().GetAsync(Query("space-station", "MIR"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var detail = await DetailOf(response);
        foreach (var kind in new[]
                 {
                     "brief", "configuration", "decision", "forecast-scope", "inventory-unit",
                     "order-configuration", "part", "procurement-item", "service-model",
                 })
        {
            Assert.Contains(kind, detail, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_known_kind_with_an_unknown_reference_is_a_not_found()
    {
        using var executive = Executive("exec-unknown-ref");
        var response = await executive.CreateClient().GetAsync(Query("inventory-unit", "STK-DOES-NOT-EXIST"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("carries a recorded explanation", await DetailOf(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_missing_kind_or_reference_is_refused()
    {
        using var executive = Executive("exec-missing-args");
        var client = executive.CreateClient();

        var noKind = await client.GetAsync($"{ExplainUrl}?ref=STK-1");
        Assert.Equal(HttpStatusCode.BadRequest, noKind.StatusCode);

        var noRef = await client.GetAsync($"{ExplainUrl}?kind=inventory-unit");
        Assert.Equal(HttpStatusCode.BadRequest, noRef.StatusCode);
        Assert.Contains("'ref'", await DetailOf(noRef), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- authorization

    /// <summary>
    /// Re-hosts with reads protected and Entra bearer auth, so a request carrying no token really is
    /// anonymous. The development provider would otherwise issue a configured principal and every
    /// "unauthenticated" assertion would silently be testing an authenticated caller.
    /// </summary>
    private WebApplicationFactory<Program> SecuredWithBearer() =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.RequireAuthenticatedReads)}", "true");
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Provider)}", nameof(AuthProvider.EntraId));
            builder.UseSetting(
                $"{AuthOptions.SectionName}:{nameof(AuthOptions.Authority)}",
                "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/v2.0");
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Audience)}", "api://beeeye-test");
        });

    [Fact]
    public async Task An_unauthenticated_read_is_refused_when_reads_require_authentication()
    {
        using var secured = SecuredWithBearer();
        var response = await secured.CreateClient().GetAsync(Query("brief", "current"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_role_without_the_view_permission_is_refused()
    {
        // IT-Admin administers the platform and holds no business-analytics permissions.
        using var itAdmin = As("it-admin-explain", PlatformRoles.ItAdmin);
        var response = await itAdmin.CreateClient().GetAsync(Query("brief", "current"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------------------------------------------------------- failure isolation

    /// <summary>A provider that always fails, registered only for the failure-isolation tests.</summary>
    private sealed class ExplodingProvider : IExplainabilityProvider
    {
        public const string Kind = "exploding-subject";

        public IReadOnlySet<string> SubjectKinds { get; } = new HashSet<string>(StringComparer.Ordinal) { Kind };

        public Task<Explanation?> ExplainAsync(string subjectKind, string subjectRef, CancellationToken ct) =>
            throw new InvalidOperationException(
                "Secret connection string: Host=db;Password=hunter2 — SELECT * FROM inventory_items");
    }

    private WebApplicationFactory<Program> WithFailingProvider(string subjectId) =>
        As(subjectId, PlatformRoles.Executive)
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.AddScoped<IExplainabilityProvider, ExplodingProvider>()));

    [Fact]
    public async Task A_failing_provider_yields_a_gap_and_a_two_hundred_rather_than_a_five_hundred()
    {
        using var host = WithFailingProvider("exec-gap");
        var response = await host.CreateClient().GetAsync(Query(ExplodingProvider.Kind, "anything"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(JsonValueKind.Null, payload.GetProperty("explanation").ValueKind);

        var gap = Assert.Single(payload.GetProperty("gaps").EnumerateArray());
        Assert.Equal(ExplodingProvider.Kind, gap.GetProperty("area").GetString());
        Assert.Contains("could not be assembled", gap.GetProperty("reason").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task No_exception_detail_appears_anywhere_in_the_body_of_a_failed_explanation()
    {
        // The twin of the cockpit's leakage test. A gap tells the reader the panel is incomplete; it
        // must never tell them the connection string.
        using var host = WithFailingProvider("exec-leak");
        var body = await host.CreateClient().GetStringAsync(Query(ExplodingProvider.Kind, "anything"));

        foreach (var secret in new[]
                 {
                     "Password", "hunter2", "connection string", "InvalidOperationException",
                     "SELECT", "inventory_items", "at BeeEye.",
                 })
        {
            Assert.DoesNotContain(secret, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---------------------------------------------------------------- feedback

    private static object Feedback(string kind, string subjectRef, string verdict, string? note = null) =>
        new { kind, @ref = subjectRef, verdict, note };

    [Fact]
    public async Task Feedback_is_recorded_and_read_back_with_the_no_retraining_caveat()
    {
        using var executive = Executive("exec-feedback-1");
        var client = executive.CreateClient();

        var response = await ReadAsync(await client.SendAsync(
            Post(FeedbackUrl, Feedback("brief", "current", "Useful", "Clear enough."), NewKey())));

        Assert.Equal("Useful", response.GetProperty("verdict").GetString());
        Assert.Contains("does not retrain", response.GetProperty("caveat").GetString()!, StringComparison.OrdinalIgnoreCase);

        var explained = await ExplainAsync(client, "brief", "current");
        Assert.Contains("does not retrain", explained.GetProperty("feedbackCaveat").GetString()!, StringComparison.OrdinalIgnoreCase);

        var mine = explained.GetProperty("feedback").EnumerateArray()
            .FirstOrDefault(f => f.GetProperty("submittedBy").GetString() == "exec-feedback-1");
        Assert.NotEqual(JsonValueKind.Undefined, mine.ValueKind);
        Assert.Equal("Useful", mine.GetProperty("verdict").GetString());
        Assert.Equal("Clear enough.", mine.GetProperty("note").GetString());
    }

    [Fact]
    public async Task A_replayed_key_returns_the_identical_answer_and_writes_exactly_one_row()
    {
        const string subjectRef = "replay-subject";
        var key = NewKey();
        var body = Feedback("brief", subjectRef, "NeedsReview");

        using var executive = Executive("exec-feedback-replay");
        var client = executive.CreateClient();

        var first = await client.SendAsync(Post(FeedbackUrl, body, key));
        var second = await client.SendAsync(Post(FeedbackUrl, body, key));

        Assert.Equal(first.StatusCode, second.StatusCode);
        Assert.Equal(
            await first.Content.ReadAsStringAsync(),
            await second.Content.ReadAsStringAsync());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();
        Assert.Equal(1, await db.ExplainabilityFeedback.AsNoTracking().CountAsync(f => f.SubjectRef == subjectRef));
    }

    [Fact]
    public async Task A_replayed_key_carrying_a_different_verdict_is_refused()
    {
        var key = NewKey();

        using var executive = Executive("exec-feedback-conflict");
        var client = executive.CreateClient();

        await client.SendAsync(Post(FeedbackUrl, Feedback("brief", "conflict-subject", "Useful"), key));
        var second = await client.SendAsync(Post(FeedbackUrl, Feedback("brief", "conflict-subject", "Incorrect"), key));

        // Same key, different intent. Replaying the stored answer would be a lie and running the new
        // one would break the client's own assumption that the key made it safe.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
    }

    [Fact]
    public async Task A_missing_or_malformed_idempotency_key_is_refused()
    {
        using var executive = Executive("exec-feedback-key");
        var client = executive.CreateClient();

        var missing = await client.SendAsync(Post(FeedbackUrl, Feedback("brief", "current", "Useful"), key: null));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var malformed = await client.SendAsync(Post(FeedbackUrl, Feedback("brief", "current", "Useful"), "  "));
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    [Fact]
    public async Task An_unknown_verdict_is_refused_with_the_valid_values()
    {
        using var executive = Executive("exec-feedback-verdict");
        var response = await executive.CreateClient().SendAsync(
            Post(FeedbackUrl, Feedback("brief", "current", "Splendid"), NewKey()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var detail = await DetailOf(response);
        foreach (var verdict in new[] { "Useful", "NeedsReview", "Incorrect", "MissingContext" })
        {
            Assert.Contains(verdict, detail, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Feedback_on_an_unknown_kind_is_refused()
    {
        using var executive = Executive("exec-feedback-kind");
        var response = await executive.CreateClient().SendAsync(
            Post(FeedbackUrl, Feedback("space-station", "MIR", "Useful"), NewKey()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("not explainable", await DetailOf(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Changing_your_mind_appends_a_second_row_and_the_latest_wins()
    {
        const string subjectRef = "append-subject";

        using var analyst = Analyst("analyst-appends");
        var client = analyst.CreateClient();

        await client.SendAsync(Post(FeedbackUrl, Feedback("brief", subjectRef, "Incorrect", "Wrong."), NewKey()));
        await client.SendAsync(Post(FeedbackUrl, Feedback("brief", subjectRef, "Useful", "Re-read it."), NewKey()));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        // Both rows survive: nothing is updated in place, because the history of how an opinion moved
        // is the part worth having when someone asks whether a change to the engine helped.
        Assert.Equal(2, await db.ExplainabilityFeedback.AsNoTracking().CountAsync(f => f.SubjectRef == subjectRef));

        var explained = await ExplainAsync(client, "brief", subjectRef);
        var mine = explained.GetProperty("feedback").EnumerateArray()
            .Where(f => f.GetProperty("submittedBy").GetString() == "analyst-appends")
            .ToList();

        // Read back as the latest per person, so the drawer shows the current opinion and not three.
        var only = Assert.Single(mine);
        Assert.Equal("Useful", only.GetProperty("verdict").GetString());
    }

    [Fact]
    public async Task Feedback_is_attributed_to_a_stable_subject_id_never_a_display_name()
    {
        const string subjectRef = "attribution-subject";

        using var executive = Executive("exec-attribution");
        await executive.CreateClient().SendAsync(
            Post(FeedbackUrl, Feedback("brief", subjectRef, "Useful"), NewKey()));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();
        var row = await db.ExplainabilityFeedback.AsNoTracking().SingleAsync(f => f.SubjectRef == subjectRef);

        Assert.Equal("exec-attribution", row.SubmittedBy);
    }

    [Fact]
    public async Task The_feedback_write_is_refused_unauthenticated_even_in_the_relaxed_read_posture()
    {
        // RequireAuthenticatedReads off is the Development default; it relaxes *reads* only. A write
        // that could be relaxed by configuration is a write with no accountability. Bearer auth with
        // no token, so the caller genuinely has no identity rather than the dev provider's.
        var relaxed = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.RequireAuthenticatedReads)}", "false");
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Provider)}", nameof(AuthProvider.EntraId));
            builder.UseSetting(
                $"{AuthOptions.SectionName}:{nameof(AuthOptions.Authority)}",
                "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/v2.0");
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Audience)}", "api://beeeye-test");
        });

        using var _ = relaxed;
        var client = relaxed.CreateClient();

        var read = await client.GetAsync(Query("brief", "current"));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var write = await client.SendAsync(Post(FeedbackUrl, Feedback("brief", "current", "Useful"), NewKey()));
        Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);
    }

    [Fact]
    public async Task A_role_without_the_feedback_permission_is_refused()
    {
        using var itAdmin = As("it-admin-feedback", PlatformRoles.ItAdmin);
        var response = await itAdmin.CreateClient().SendAsync(
            Post(FeedbackUrl, Feedback("brief", "current", "Useful"), NewKey()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_over_long_note_is_refused_before_the_database_sees_it()
    {
        using var executive = Executive("exec-feedback-long");
        var response = await executive.CreateClient().SendAsync(
            Post(FeedbackUrl, Feedback("brief", "current", "Useful", new string('x', 1_001)), NewKey()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("1000 characters", await DetailOf(response), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- no delete path

    [Fact]
    public async Task There_is_no_delete_route_under_the_explainability_endpoints()
    {
        using var executive = Executive("exec-no-delete");
        var client = executive.CreateClient();

        foreach (var url in new[] { ExplainUrl, FeedbackUrl, $"{FeedbackUrl}/{Guid.NewGuid()}" })
        {
            var response = await client.DeleteAsync(url);

            Assert.True(
                response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
                $"DELETE {url} returned {(int)response.StatusCode}; feedback is append-only.");
        }
    }

    [Fact]
    public async Task The_published_api_document_declares_no_delete_under_the_explain_routes()
    {
        // Asserted against the served document rather than the source, so a route added anywhere —
        // including by a future module — is caught.
        using var client = factory.CreateClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        foreach (var path in document.GetProperty("paths").EnumerateObject())
        {
            if (!path.Name.StartsWith(ExplainUrl, StringComparison.Ordinal))
            {
                continue;
            }

            Assert.False(
                path.Value.TryGetProperty("delete", out _),
                $"{path.Name} declares a DELETE operation; explainability feedback is append-only.");
        }
    }

    // ---------------------------------------------------------------- schema

    [Fact]
    public async Task The_migration_created_the_feedback_table_and_left_the_fifteen_existing_ones_alone()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        var tables = await db.Database
            .SqlQuery<string>($"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync();

        Assert.Contains("explainability_feedback", tables);

        // The fifteen tables that existed before S3, still present and still named the same.
        foreach (var existing in new[]
                 {
                     "sales_facts", "inventory_items", "ingestion_batches", "vehicle_sales", "service_events",
                     "parts", "part_compatibilities", "part_supersessions", "part_usages",
                     "recommendations", "recommendation_status_events",
                     "management_decisions", "approval_steps", "action_outcomes", "idempotency_records",
                 })
        {
            Assert.Contains(existing, tables);
        }
    }

    // ---------------------------------------------------------------- performance

    [Fact]
    public async Task Explaining_an_inventory_unit_responds_within_the_budget()
    {
        using var executive = Executive("exec-budget");
        var client = executive.CreateClient();

        var stockId = await FirstAsync(client, "/api/v1/inventory/items?pageSize=1", i => i.GetProperty("stockId").GetString()!);
        Assert.NotNull(stockId);

        // Warm the caches so this measures steady-state, not first-request compilation.
        await client.GetStringAsync(Query("inventory-unit", stockId));

        var sw = Stopwatch.StartNew();
        await client.GetStringAsync(Query("inventory-unit", stockId));
        sw.Stop();

        Assert.True(
            sw.ElapsedMilliseconds < 5_000,
            $"explaining an inventory unit took {sw.ElapsedMilliseconds}ms, exceeding the 5000ms budget");
    }

    [Fact]
    public async Task Explaining_a_part_responds_within_the_budget()
    {
        // UC7 is one of the two endpoints V3-PERF-001 already flags as slow, and the provider goes
        // through the same recomputation. Held to the same budget as everything else rather than
        // given a wider one — a wider budget would hide the fix S8 is supposed to prove.
        using var executive = Executive("exec-budget-part");
        var client = executive.CreateClient();

        var partNumber = await FirstAsync(
            client, "/api/v1/spare-parts/demand/parts?pageSize=1", p => p.GetProperty("partNumber").GetString()!, "page");
        Assert.NotNull(partNumber);

        await client.GetStringAsync(Query("part", partNumber));

        var sw = Stopwatch.StartNew();
        await client.GetStringAsync(Query("part", partNumber));
        sw.Stop();

        Assert.True(
            sw.ElapsedMilliseconds < 5_000,
            $"explaining a part took {sw.ElapsedMilliseconds}ms, exceeding the 5000ms budget");
    }
}
