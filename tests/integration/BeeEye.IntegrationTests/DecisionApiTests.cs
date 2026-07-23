using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BeeEye.Persistence;
using BeeEye.Persistence.Entities;
using BeeEye.Shared.Decisions;
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
/// End-to-end tests for the governed decision log and the human decision workflow
/// (<c>docs/adr/0006-recommendation-decision-workflow.md</c>, S6).
/// <para>
/// The properties these exist to prove are the ones the ADR was written for: the frozen recommendation
/// is never mutated, every transition appends an event, a decision names a human, no single person can
/// both decide and approve, a duplicate submission produces one effect, and <b>there is no delete
/// path</b>.
/// </para>
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class DecisionApiTests(IntegrationTestFactory factory)
{
    private const string DecisionsUrl = "/api/v1/decisions";
    private const string GenerateUrl = "/api/v1/recommendations/records/generate";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ---------------------------------------------------------------- hosting helpers

    /// <summary>
    /// Re-hosts with authorization enforced and the caller holding the given roles, plus a distinct
    /// subject id — segregation of duties is about <i>people</i>, so the tests need more than one.
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

    private WebApplicationFactory<Program> Analyst(string subjectId = "analyst-1") =>
        As(subjectId, PlatformRoles.Analyst);

    private WebApplicationFactory<Program> Executive(string subjectId = "exec-1") =>
        As(subjectId, PlatformRoles.Executive);

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

    // ---------------------------------------------------------------- fixture

    /// <summary>
    /// The engine action every seeded record carries. It states a quantity (40 units) and a discount
    /// (15%) explicitly, because the modification rules verify a delta's <c>from</c> against the value
    /// the engine actually recommended — so the fixture has to be a record whose recommended values are
    /// knowable, exactly as a real one is.
    /// </summary>
    private const string SeededAction =
        "Transfer 40 unit(s) Riyadh → Jeddah and apply a controlled discount of 15%.";

    /// <summary>
    /// Inserts one frozen recommendation with its opening status event, mirroring what a generation run
    /// writes.
    /// <para>
    /// Seeded rather than generated because a run produces a fixed handful of records for the sample
    /// dataset, and is idempotent — so a suite that claimed them would exhaust the supply and every
    /// later test would fail for a reason that has nothing to do with what it was testing. Each test
    /// gets its own record and cannot be disturbed by any other.
    /// </para>
    /// </summary>
    private async Task<Guid> SeedRecommendationAsync(string? action = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        var now = DateTimeOffset.UtcNow;
        var recommendation = new Recommendation
        {
            Id = Guid.CreateVersion7(),
            IdempotencyKey = $"test|{Guid.NewGuid()}",
            SubjectRef = "ES 350 ZX · Pearl White",
            Area = "Inventory",
            RuleId = "D-INV-1",
            Action = action ?? SeededAction,
            Rationale = "Riyadh holds units the risk model recommends moving before they age further.",
            EvidenceJson = """["3 unit(s) · 540,000 SAR stock value"]""",
            ExpectedOutcome = "Historically moved more volume in the receiving region.",
            Confidence = "Medium",
            AssumptionsJson = """["Inventory metrics reflect the analysis-date assumption."]""",
            ImpactSar = 18_615m,
            Priority = 42,
            OwnerRole = "Inventory Manager",
            IsDemoData = false,
            RulesetVersion = "v1",
            DatasetVersion = "test-dataset",
            AnalysisDate = new DateOnly(2026, 6, 30),
            CurrentStatus = RecommendationStatus.Generated,
            ValidUntilUtc = now.AddDays(30),
            CreatedAtUtc = now,
        };

        recommendation.StatusEvents.Add(new RecommendationStatusEvent
        {
            Id = Guid.CreateVersion7(),
            RecommendationId = recommendation.Id,
            FromStatus = null,
            ToStatus = RecommendationStatus.Generated,
            Actor = "system",
            Reason = "Generated by the rule engine.",
            AtUtc = now,
        });

        db.Recommendations.Add(recommendation);
        await db.SaveChangesAsync();

        return recommendation.Id;
    }

    /// <summary>Runs a real generation, so at least some rows in the log came from the engine.</summary>
    private async Task GenerateAsync()
    {
        using var analyst = Analyst();
        (await analyst.CreateClient().PostAsync(GenerateUrl, null)).EnsureSuccessStatusCode();
    }

    /// <summary>Claims a fresh recommendation as the analyst and returns its decision id.</summary>
    private async Task<(Guid RecommendationId, Guid DecisionId)> ClaimedAsync(string subjectId = "analyst-1")
    {
        var recommendationId = await SeedRecommendationAsync();

        using var analyst = Analyst(subjectId);
        var response = await analyst.CreateClient().SendAsync(
            Post($"{DecisionsUrl}/recommendations/{recommendationId}/claim", null, NewKey()));

        var body = await ReadAsync(response);
        return (recommendationId, body.GetProperty("decisionId").GetGuid());
    }

    // ---------------------------------------------------------------- the full lifecycle

    [Fact]
    public async Task A_recommendation_runs_the_full_lifecycle_and_the_log_records_every_step()
    {
        var (recommendationId, decisionId) = await ClaimedAsync("analyst-lifecycle");

        using var approver = Executive("exec-lifecycle");
        var approverClient = approver.CreateClient();

        // The approver accepts with a change...
        var accepted = await approverClient.SendAsync(Post(
            $"{DecisionsUrl}/{decisionId}/accept-with-modification",
            new { field = "proposed_qty", from = 40m, to = 30m, rationale = "Trimmed for showroom capacity" },
            NewKey()));

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        // ...a *second* person signs the chain off...
        using var secondApprover = Executive("exec-second");
        var signOff = await secondApprover.CreateClient().SendAsync(Post(
            $"{DecisionsUrl}/{decisionId}/approvals/1", new { approved = true, note = "Agreed" }, NewKey()));

        Assert.Equal(HttpStatusCode.OK, signOff.StatusCode);

        // ...a human confirms it was executed downstream...
        var implemented = await approverClient.SendAsync(
            Post($"{DecisionsUrl}/{decisionId}/implemented", null, NewKey()));

        Assert.Equal(HttpStatusCode.OK, implemented.StatusCode);

        // ...and the realised effect is measured, closing the loop.
        var outcome = await approverClient.SendAsync(Post(
            $"{DecisionsUrl}/{decisionId}/outcome",
            new { metric = "Holding cost avoided", realisedValue = 18_615.50m, unit = "SAR", note = "Measured at month end" },
            NewKey()));

        Assert.Equal(HttpStatusCode.OK, outcome.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        var events = await db.RecommendationStatusEvents
            .AsNoTracking()
            .Where(e => e.RecommendationId == recommendationId)
            .OrderBy(e => e.AtUtc).ThenBy(e => e.Id)
            .ToListAsync();

        Assert.Equal(
            new[]
            {
                RecommendationStatus.Generated,
                RecommendationStatus.UnderReview,
                RecommendationStatus.AcceptedModified,
                RecommendationStatus.Implemented,
                RecommendationStatus.OutcomeRecorded,
            },
            events.Select(e => e.ToStatus));

        // Each transition names the actor who caused it — the whole point of the trail.
        Assert.Equal("system", events[0].Actor);
        Assert.Equal("analyst-lifecycle", events[1].Actor);
        Assert.Equal("exec-lifecycle", events[2].Actor);

        // Every step's from-status is the previous step's to-status: an unbroken chain.
        for (var i = 1; i < events.Count; i++)
        {
            Assert.Equal(events[i - 1].ToStatus, events[i].FromStatus);
        }

        var record = await db.Recommendations.AsNoTracking().SingleAsync(r => r.Id == recommendationId);
        Assert.Equal(RecommendationStatus.OutcomeRecorded, record.CurrentStatus);

        var decision = await db.ManagementDecisions
            .AsNoTracking()
            .Include(d => d.ActionOutcome)
            .Include(d => d.ApprovalSteps)
            .SingleAsync(d => d.Id == decisionId);

        Assert.Equal(DecisionOutcome.AcceptedModified, decision.Outcome);
        Assert.Equal("exec-lifecycle", decision.DecidedBy);
        Assert.Equal("exec-second", Assert.Single(decision.ApprovalSteps).ActedBy);
        Assert.Equal(18_615.50m, decision.ActionOutcome!.RealisedValue);
    }

    [Fact]
    public async Task The_projected_status_agrees_with_the_log_at_every_step()
    {
        var (recommendationId, decisionId) = await ClaimedAsync("analyst-projection");

        await AssertProjectionAgreesAsync(recommendationId);

        using var approver = Executive("exec-projection");
        await approver.CreateClient().SendAsync(Post($"{DecisionsUrl}/{decisionId}/accept", null, NewKey()));
        await AssertProjectionAgreesAsync(recommendationId);

        await approver.CreateClient().SendAsync(Post($"{DecisionsUrl}/{decisionId}/implemented", null, NewKey()));
        await AssertProjectionAgreesAsync(recommendationId);
    }

    private async Task AssertProjectionAgreesAsync(Guid recommendationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        var record = await db.Recommendations
            .AsNoTracking()
            .Include(r => r.StatusEvents)
            .SingleAsync(r => r.Id == recommendationId);

        var latest = record.StatusEvents.OrderByDescending(e => e.AtUtc).ThenByDescending(e => e.Id).First();
        Assert.Equal(latest.ToStatus, record.CurrentStatus);
    }

    // ---------------------------------------------------------------- the original is never mutated

    [Fact]
    public async Task Deciding_never_mutates_the_frozen_recommendation()
    {
        var recommendationId = await SeedRecommendationAsync();
        var before = await SnapshotAsync(recommendationId);

        var (_, decisionId) = await ClaimAsync(recommendationId, "analyst-frozen");

        using var approver = Executive("exec-frozen");
        await approver.CreateClient().SendAsync(Post(
            $"{DecisionsUrl}/{decisionId}/accept-with-modification",
            new { field = "transfer_qty", from = 40m, to = 1m, rationale = "Only one is movable this week" },
            NewKey()));

        var after = await SnapshotAsync(recommendationId);

        // Everything the engine wrote is byte-for-byte unchanged. Only CurrentStatus moved, and only
        // through the transition service.
        Assert.Equal(before with { CurrentStatus = after.CurrentStatus, Version = after.Version }, after);
        Assert.Equal(RecommendationStatus.AcceptedModified.ToString(), after.CurrentStatus);
    }

    [Fact]
    public async Task A_record_nobody_touches_keeps_even_its_row_version()
    {
        var recommendationId = await SeedRecommendationAsync();
        var before = await SnapshotAsync(recommendationId);

        // Someone else's decision, entirely.
        await ClaimedAsync("analyst-elsewhere");

        Assert.Equal(before, await SnapshotAsync(recommendationId));
    }

    /// <summary>Every column of the frozen record, for a before/after comparison.</summary>
    private sealed record Snapshot(
        Guid Id, string IdempotencyKey, string SubjectRef, string Area, string RuleId, string Action,
        string Rationale, string EvidenceJson, string ExpectedOutcome, string Confidence,
        string AssumptionsJson, decimal ImpactSar, int Priority, string OwnerRole, bool IsDemoData,
        string RulesetVersion, string DatasetVersion, DateOnly AnalysisDate, string CurrentStatus,
        DateTimeOffset? ValidUntilUtc, Guid? SupersededBy, DateTimeOffset CreatedAtUtc, uint Version);

    private async Task<Snapshot> SnapshotAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();
        var r = await db.Recommendations.AsNoTracking().SingleAsync(x => x.Id == id);

        return new Snapshot(
            r.Id, r.IdempotencyKey, r.SubjectRef, r.Area, r.RuleId, r.Action, r.Rationale, r.EvidenceJson,
            r.ExpectedOutcome, r.Confidence, r.AssumptionsJson, r.ImpactSar, r.Priority, r.OwnerRole,
            r.IsDemoData, r.RulesetVersion, r.DatasetVersion, r.AnalysisDate, r.CurrentStatus.ToString(),
            r.ValidUntilUtc, r.SupersededByRecommendationId, r.CreatedAtUtc, r.Version);
    }

    private async Task<(Guid RecommendationId, Guid DecisionId)> ClaimAsync(Guid recommendationId, string subject)
    {
        using var analyst = Analyst(subject);
        var body = await ReadAsync(await analyst.CreateClient().SendAsync(
            Post($"{DecisionsUrl}/recommendations/{recommendationId}/claim", null, NewKey())));

        return (recommendationId, body.GetProperty("decisionId").GetGuid());
    }

    // ---------------------------------------------------------------- authorization

    [Fact]
    public async Task An_analyst_cannot_accept_a_recommendation()
    {
        var (_, decisionId) = await ClaimedAsync("analyst-cannot-accept");

        using var analyst = Analyst("analyst-cannot-accept");
        var response = await analyst.CreateClient().SendAsync(
            Post($"{DecisionsUrl}/{decisionId}/accept", null, NewKey()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_it_admin_can_neither_claim_nor_accept()
    {
        var recommendationId = await SeedRecommendationAsync();
        var (_, decisionId) = await ClaimedAsync("analyst-vs-admin");

        using var admin = As("admin-1", PlatformRoles.ItAdmin);
        var client = admin.CreateClient();

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.SendAsync(Post($"{DecisionsUrl}/recommendations/{recommendationId}/claim", null, NewKey()))).StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.SendAsync(Post($"{DecisionsUrl}/{decisionId}/accept", null, NewKey()))).StatusCode);
    }

    [Fact]
    public async Task Reading_the_log_requires_the_review_permission()
    {
        using var admin = As("admin-2", PlatformRoles.ItAdmin);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.CreateClient().GetAsync(DecisionsUrl)).StatusCode);

        using var executive = Executive("exec-read");
        Assert.Equal(HttpStatusCode.OK, (await executive.CreateClient().GetAsync(DecisionsUrl)).StatusCode);
    }

    [Fact]
    public async Task A_write_is_refused_unauthenticated_even_in_the_relaxed_read_posture()
    {
        var (_, decisionId) = await ClaimedAsync("analyst-anon");

        // The unmodified Development factory relaxes *reads*. A write must still be refused, because
        // no configuration setting may open a state-changing path (ADR 0008 §2.4).
        using var secured = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Provider)}", nameof(AuthProvider.EntraId));
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Authority)}", "https://login.microsoftonline.com/t/v2.0");
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Audience)}", "api://beeeye-test");
        });

        var response = await secured.CreateClient().SendAsync(
            Post($"{DecisionsUrl}/{decisionId}/accept", null, NewKey()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Recording_an_outcome_needs_its_own_permission()
    {
        var (_, decisionId) = await ClaimedAsync("analyst-outcome-perm");

        // The IT admin holds neither approval nor outcome-recording authority.
        using var admin = As("admin-3", PlatformRoles.ItAdmin);
        var response = await admin.CreateClient().SendAsync(Post(
            $"{DecisionsUrl}/{decisionId}/outcome",
            new { metric = "Anything", realisedValue = 1m, unit = "SAR", note = (string?)null },
            NewKey()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------------------------------------------------------- segregation of duties

    [Fact]
    public async Task The_person_who_decided_cannot_sign_their_own_approval_off()
    {
        var (_, decisionId) = await ClaimedAsync("analyst-self");

        using var approver = Executive("exec-self");
        var client = approver.CreateClient();

        await client.SendAsync(Post($"{DecisionsUrl}/{decisionId}/accept", null, NewKey()));

        var response = await client.SendAsync(Post(
            $"{DecisionsUrl}/{decisionId}/approvals/1", new { approved = true, note = (string?)null }, NewKey()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("second person", await DetailOf(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_different_person_may_sign_the_same_approval_off()
    {
        var (_, decisionId) = await ClaimedAsync("analyst-second");

        using var decider = Executive("exec-decider");
        await decider.CreateClient().SendAsync(Post($"{DecisionsUrl}/{decisionId}/accept", null, NewKey()));

        using var other = Executive("exec-other");
        var response = await other.CreateClient().SendAsync(Post(
            $"{DecisionsUrl}/{decisionId}/approvals/1", new { approved = true, note = (string?)null }, NewKey()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_step_already_acted_on_is_immutable()
    {
        var (_, decisionId) = await ClaimedAsync("analyst-immutable");

        using var decider = Executive("exec-immutable");
        await decider.CreateClient().SendAsync(Post($"{DecisionsUrl}/{decisionId}/accept", null, NewKey()));

        using var other = Executive("exec-signer");
        var client = other.CreateClient();

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.SendAsync(Post($"{DecisionsUrl}/{decisionId}/approvals/1", new { approved = true, note = (string?)null }, NewKey()))).StatusCode);

        // Re-acting is a conflict, never an overwrite: who actually approved is the fact the chain exists to record.
        var again = await client.SendAsync(Post(
            $"{DecisionsUrl}/{decisionId}/approvals/1", new { approved = false, note = "Changed my mind" }, NewKey()));

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task A_declined_step_blocks_the_decision_from_being_marked_implemented()
    {
        var (_, decisionId) = await ClaimedAsync("analyst-declined");

        using var decider = Executive("exec-declined-decider");
        await decider.CreateClient().SendAsync(Post($"{DecisionsUrl}/{decisionId}/accept", null, NewKey()));

        using var other = Executive("exec-declined-signer");
        await other.CreateClient().SendAsync(Post(
            $"{DecisionsUrl}/{decisionId}/approvals/1", new { approved = false, note = "Not this quarter" }, NewKey()));

        var response = await decider.CreateClient().SendAsync(
            Post($"{DecisionsUrl}/{decisionId}/implemented", null, NewKey()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("declined", await DetailOf(response), StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- validation, server-side

    [Fact]
    public async Task Rejecting_without_a_note_is_refused_with_the_state_machines_own_wording()
    {
        var (_, decisionId) = await ClaimedAsync("analyst-note");

        using var approver = Executive("exec-note");
        var response = await approver.CreateClient().SendAsync(Post(
            $"{DecisionsUrl}/{decisionId}/reject", new { note = "   " }, NewKey()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("reason is required", await DetailOf(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejecting_with_a_note_reaches_the_terminal_state_and_keeps_the_reason()
    {
        var (recommendationId, decisionId) = await ClaimedAsync("analyst-reject");

        using var approver = Executive("exec-reject");
        var response = await approver.CreateClient().SendAsync(Post(
            $"{DecisionsUrl}/{decisionId}/reject", new { note = "Showroom cannot absorb the units" }, NewKey()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        var decision = await db.ManagementDecisions.AsNoTracking().SingleAsync(d => d.Id == decisionId);
        Assert.Equal(DecisionOutcome.Rejected, decision.Outcome);
        Assert.Equal("Showroom cannot absorb the units", decision.Note);

        var record = await db.Recommendations.AsNoTracking().SingleAsync(r => r.Id == recommendationId);
        Assert.Equal(RecommendationStatus.Rejected, record.CurrentStatus);
    }

    [Fact]
    public async Task Any_transition_out_of_a_rejected_record_is_refused_as_terminal()
    {
        var (_, decisionId) = await ClaimedAsync("analyst-terminal");

        using var approver = Executive("exec-terminal");
        var client = approver.CreateClient();

        await client.SendAsync(Post($"{DecisionsUrl}/{decisionId}/reject", new { note = "No" }, NewKey()));

        var response = await client.SendAsync(Post($"{DecisionsUrl}/{decisionId}/implemented", null, NewKey()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("final state", await DetailOf(response), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("selling_price", 100, 90, HttpStatusCode.BadRequest)]
    [InlineData("proposed_qty", 40, 40, HttpStatusCode.BadRequest)]
    [InlineData("transfer_qty", 40, -1, HttpStatusCode.BadRequest)]
    [InlineData("discount_pct", 15, 25, HttpStatusCode.UnprocessableEntity)]
    [InlineData("discount_pct", 15, -0.1, HttpStatusCode.UnprocessableEntity)]
    public async Task Every_modification_rule_is_enforced_over_http_not_only_in_the_browser(
        string field, decimal from, decimal to, HttpStatusCode expected)
    {
        var (_, decisionId) = await ClaimedAsync($"analyst-mod-{field}-{to}");

        using var approver = Executive($"exec-mod-{field}-{to}");
        var response = await approver.CreateClient().SendAsync(Post(
            $"{DecisionsUrl}/{decisionId}/accept-with-modification",
            new { field, from, to, rationale = (string?)null },
            NewKey()));

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(10)]
    public async Task A_discount_inside_the_observed_band_is_accepted(decimal to)
    {
        var (_, decisionId) = await ClaimedAsync($"analyst-band-{to}");

        using var approver = Executive($"exec-band-{to}");
        var response = await approver.CreateClient().SendAsync(Post(
            $"{DecisionsUrl}/{decisionId}/accept-with-modification",
            new { field = "discount_pct", from = 15m, to, rationale = (string?)null },
            NewKey()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Modifying_a_value_the_engine_never_recommended_is_refused_as_stale()
    {
        // The seeded record recommends 40 units. A client sending from=55 is working from a copy of
        // the record that has since moved on, and must reload rather than have its number accepted.
        var (_, decisionId) = await ClaimedAsync("analyst-stale");

        using var approver = Executive("exec-stale");
        var response = await approver.CreateClient().SendAsync(Post(
            $"{DecisionsUrl}/{decisionId}/accept-with-modification",
            new { field = "proposed_qty", from = 55m, to = 30m, rationale = (string?)null },
            NewKey()));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("Reload", await DetailOf(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reducing_a_quantity_to_zero_is_a_legitimate_decision()
    {
        var (_, decisionId) = await ClaimedAsync("analyst-zero");

        using var approver = Executive("exec-zero");
        var response = await approver.CreateClient().SendAsync(Post(
            $"{DecisionsUrl}/{decisionId}/accept-with-modification",
            new { field = "procurement_qty", from = 40m, to = 0m, rationale = "Pause entirely" },
            NewKey()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_decided_recommendation_cannot_be_decided_twice()
    {
        var (_, decisionId) = await ClaimedAsync("analyst-twice");

        using var approver = Executive("exec-twice");
        var client = approver.CreateClient();

        await client.SendAsync(Post($"{DecisionsUrl}/{decisionId}/accept", null, NewKey()));

        var second = await client.SendAsync(Post($"{DecisionsUrl}/{decisionId}/reject", new { note = "Actually no" }, NewKey()));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task An_unknown_id_is_a_404_not_a_500()
    {
        using var approver = Executive("exec-404");
        var client = approver.CreateClient();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.SendAsync(Post($"{DecisionsUrl}/{Guid.NewGuid()}/accept", null, NewKey()))).StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"{DecisionsUrl}/{Guid.NewGuid()}")).StatusCode);
    }

    // ---------------------------------------------------------------- claiming

    [Fact]
    public async Task Four_concurrent_claims_of_one_recommendation_produce_exactly_one_decision()
    {
        var recommendationId = await SeedRecommendationAsync();

        using var analyst = Analyst("analyst-race");
        var client = analyst.CreateClient();

        // Distinct keys, so this exercises the *filtered unique index*, not the idempotency store:
        // four genuinely different intents racing for one recommendation.
        var responses = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => client.SendAsync(
                Post($"{DecisionsUrl}/recommendations/{recommendationId}/claim", null, NewKey()))));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(3, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        Assert.Equal(
            1, await db.ManagementDecisions.AsNoTracking().CountAsync(d => d.RecommendationId == recommendationId));
    }

    [Fact]
    public async Task A_claim_seeds_one_pending_approval_step_from_the_owner_role()
    {
        var (_, decisionId) = await ClaimedAsync("analyst-seed");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        var step = await db.ApprovalSteps.AsNoTracking().SingleAsync(s => s.DecisionId == decisionId);

        Assert.Equal(1, step.StepNumber);
        Assert.Equal(ApprovalStepStatus.Pending, step.Status);
        Assert.False(string.IsNullOrWhiteSpace(step.ApproverRole));
    }

    [Fact]
    public async Task A_claimed_recommendation_cannot_be_claimed_again()
    {
        var (recommendationId, _) = await ClaimedAsync("analyst-reclaim");

        using var other = Analyst("analyst-reclaim-other");
        var response = await other.CreateClient().SendAsync(
            Post($"{DecisionsUrl}/recommendations/{recommendationId}/claim", null, NewKey()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("already reviewing", await DetailOf(response), StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- idempotency (ADR 0007 §2.1)

    [Fact]
    public async Task A_replayed_key_returns_the_identical_response_and_creates_one_decision()
    {
        var recommendationId = await SeedRecommendationAsync();
        var key = NewKey();

        using var analyst = Analyst("analyst-replay");
        var client = analyst.CreateClient();
        var url = $"{DecisionsUrl}/recommendations/{recommendationId}/claim";

        var first = await client.SendAsync(Post(url, null, key));
        var second = await client.SendAsync(Post(url, null, key));

        Assert.Equal(first.StatusCode, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(
            await first.Content.ReadAsStringAsync(),
            await second.Content.ReadAsStringAsync());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        Assert.Equal(
            1, await db.ManagementDecisions.AsNoTracking().CountAsync(d => d.RecommendationId == recommendationId));
    }

    [Fact]
    public async Task Reusing_a_key_for_a_different_body_is_unprocessable()
    {
        var (_, decisionId) = await ClaimedAsync("analyst-reuse");
        var key = NewKey();

        using var approver = Executive("exec-reuse");
        var client = approver.CreateClient();
        var url = $"{DecisionsUrl}/{decisionId}/accept-with-modification";

        var first = await client.SendAsync(Post(
            url, new { field = "proposed_qty", from = 40m, to = 30m, rationale = (string?)null }, key));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.SendAsync(Post(
            url, new { field = "proposed_qty", from = 40m, to = 20m, rationale = (string?)null }, key));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
    }

    [Fact]
    public async Task Reordering_the_body_of_a_retry_is_still_the_same_request()
    {
        var (_, decisionId) = await ClaimedAsync("analyst-reorder");
        var key = NewKey();

        using var approver = Executive("exec-reorder");
        var client = approver.CreateClient();
        var url = $"{DecisionsUrl}/{decisionId}/accept-with-modification";

        var first = await client.SendAsync(Post(
            url, new { field = "proposed_qty", from = 40m, to = 30m, rationale = "Capacity" }, key));

        // Same values, different property order — exactly what a retried request can look like.
        var retry = await client.SendAsync(Post(
            url, new { rationale = "Capacity", to = 30m, from = 40m, field = "proposed_qty" }, key));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await retry.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_missing_idempotency_key_is_refused_and_the_header_is_named()
    {
        var recommendationId = await SeedRecommendationAsync();

        using var analyst = Analyst("analyst-nokey");
        var response = await analyst.CreateClient().SendAsync(
            Post($"{DecisionsUrl}/recommendations/{recommendationId}/claim", null, key: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(IdempotencyKey.HeaderName, await DetailOf(response), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("has spaces")]
    public async Task A_malformed_idempotency_key_is_refused(string key)
    {
        var recommendationId = await SeedRecommendationAsync();

        using var analyst = Analyst("analyst-badkey");
        var response = await analyst.CreateClient().SendAsync(
            Post($"{DecisionsUrl}/recommendations/{recommendationId}/claim", null, key));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_refused_request_does_not_burn_its_key()
    {
        // A 400 changed nothing, so the client may fix the problem and retry with the same key. If the
        // failure were cached, a correctable mistake would become permanent.
        var (_, decisionId) = await ClaimedAsync("analyst-burn");
        var key = NewKey();

        using var approver = Executive("exec-burn");
        var client = approver.CreateClient();
        var url = $"{DecisionsUrl}/{decisionId}/reject";

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.SendAsync(Post(url, new { note = "" }, key))).StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.SendAsync(Post(url, new { note = "A real reason this time" }, key))).StatusCode);
    }

    [Fact]
    public async Task A_rolled_back_key_leaves_no_decision_behind()
    {
        // Forces the failure ADR 0007 §2.1 is built around: the effect is written, then recording the
        // key fails. Neither may survive — a persisted decision whose key was lost would be applied a
        // second time by the very next retry.
        var recommendationId = await SeedRecommendationAsync();

        using var broken = Analyst("analyst-rollback").WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<IIdempotencyStore, FailsToCommitStore>()));

        var response = await broken.CreateClient().SendAsync(
            Post($"{DecisionsUrl}/recommendations/{recommendationId}/claim", null, NewKey()));

        Assert.False(response.IsSuccessStatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        Assert.False(await db.ManagementDecisions.AsNoTracking().AnyAsync(d => d.RecommendationId == recommendationId));
        Assert.Equal(
            RecommendationStatus.Generated,
            (await db.Recommendations.AsNoTracking().SingleAsync(r => r.Id == recommendationId)).CurrentStatus);
    }

    /// <summary>
    /// An idempotency store that does everything correctly right up to the commit, then fails. Stands
    /// in for a database going away between the effect and the key row.
    /// </summary>
    private sealed class FailsToCommitStore(
        BeeEyeDbContext db, BeeEye.Shared.Time.IClock clock) : IIdempotencyStore
    {
        private readonly BeeEye.Persistence.Idempotency.EfIdempotencyStore _inner = new(db, clock);

        public Task<IdempotencyEntry?> FindAsync(string key, CancellationToken cancellationToken) =>
            _inner.FindAsync(key, cancellationToken);

        public Task BeginAsync(CancellationToken cancellationToken) => _inner.BeginAsync(cancellationToken);

        public Task<bool> TryCompleteAsync(IdempotencyEntry entry, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated failure while recording the idempotency key.");

        public Task RollbackAsync(CancellationToken cancellationToken) => _inner.RollbackAsync(cancellationToken);
    }

    // ---------------------------------------------------------------- no delete path

    [Fact]
    public async Task There_is_no_delete_route_on_a_decision()
    {
        var (recommendationId, decisionId) = await ClaimedAsync("analyst-nodelete");

        using var approver = Executive("exec-nodelete");
        var client = approver.CreateClient();

        foreach (var url in new[] { $"{DecisionsUrl}/{decisionId}", $"{DecisionsUrl}/{recommendationId}" })
        {
            var response = await client.DeleteAsync(url);

            Assert.True(
                response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
                $"DELETE {url} returned {(int)response.StatusCode}; there must be no delete path (ADR 0006).");
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();
        Assert.True(await db.ManagementDecisions.AsNoTracking().AnyAsync(d => d.Id == decisionId));
    }

    [Fact]
    public async Task The_published_api_document_declares_no_delete_under_decisions()
    {
        // Asserted against the served document rather than the source, so a route added anywhere —
        // including by a future module — is caught.
        using var client = factory.CreateClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        foreach (var path in document.GetProperty("paths").EnumerateObject())
        {
            if (!path.Name.StartsWith("/api/v1/decisions", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.False(
                path.Value.TryGetProperty("delete", out _),
                $"{path.Name} declares a DELETE operation; ADR 0006 permits no delete path.");
        }
    }

    // ---------------------------------------------------------------- the log query

    [Fact]
    public async Task The_log_returns_rows_with_status_counts_for_the_chip_row()
    {
        // Real engine output, not only seeded fixtures: the log has to render what generation writes.
        await GenerateAsync();

        using var executive = Executive("exec-log");
        var page = await ReadAsync(await executive.CreateClient().GetAsync(DecisionsUrl));

        Assert.NotEmpty(page.GetProperty("items").EnumerateArray());

        var counts = page.GetProperty("statusCounts");
        foreach (var status in Enum.GetNames<RecommendationStatus>())
        {
            Assert.True(counts.TryGetProperty(status, out var count), $"No count for {status}");
            Assert.True(count.GetInt32() >= 0);
        }
    }

    [Fact]
    public async Task Filtering_by_status_narrows_the_rows_but_not_the_counts()
    {
        await ClaimedAsync("analyst-counts");

        using var executive = Executive("exec-counts");
        var client = executive.CreateClient();

        var all = await ReadAsync(await client.GetAsync(DecisionsUrl));
        var filtered = await ReadAsync(await client.GetAsync($"{DecisionsUrl}?status=UnderReview"));

        Assert.All(
            filtered.GetProperty("items").EnumerateArray(),
            item => Assert.Equal("UnderReview", item.GetProperty("status").GetString()));

        // The chip row must keep showing every status, or selecting one would strand the user.
        Assert.Equal(
            all.GetProperty("statusCounts").GetProperty("Generated").GetInt32(),
            filtered.GetProperty("statusCounts").GetProperty("Generated").GetInt32());

        Assert.True(filtered.GetProperty("statusCounts").GetProperty("UnderReview").GetInt32() > 0);
    }

    [Theory]
    [InlineData("Generated")]
    [InlineData("UnderReview")]
    [InlineData("Accepted")]
    [InlineData("AcceptedModified")]
    [InlineData("Rejected")]
    [InlineData("Expired")]
    [InlineData("Superseded")]
    [InlineData("Implemented")]
    [InlineData("OutcomeRecorded")]
    public async Task Every_status_is_a_valid_filter(string status)
    {
        using var executive = Executive("exec-filters");
        var response = await executive.CreateClient().GetAsync($"{DecisionsUrl}?status={status}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_status_is_a_client_error_listing_the_valid_values()
    {
        using var executive = Executive("exec-badfilter");
        var response = await executive.CreateClient().GetAsync($"{DecisionsUrl}?status=Snoozed");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var detail = await DetailOf(response);
        Assert.Contains("Generated", detail, StringComparison.Ordinal);
        Assert.Contains("OutcomeRecorded", detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_outcome_filter_is_a_client_error()
    {
        using var executive = Executive("exec-badoutcome");
        var response = await executive.CreateClient().GetAsync($"{DecisionsUrl}?outcome=Assigned");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Page_size_is_clamped_and_paging_is_deterministic()
    {
        await SeedRecommendationAsync();

        using var executive = Executive("exec-paging");
        var client = executive.CreateClient();

        var huge = await ReadAsync(await client.GetAsync($"{DecisionsUrl}?pageSize=100000"));
        Assert.True(huge.GetProperty("pageSize").GetInt32() <= 200);

        var first = await ReadAsync(await client.GetAsync($"{DecisionsUrl}?page=1&pageSize=2"));
        var again = await ReadAsync(await client.GetAsync($"{DecisionsUrl}?page=1&pageSize=2"));

        Assert.Equal(
            first.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("recommendationId").GetGuid()),
            again.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("recommendationId").GetGuid()));
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?page=-5")]
    [InlineData("?pageSize=0")]
    public async Task Out_of_range_paging_is_clamped_rather_than_erroring(string query)
    {
        using var executive = Executive("exec-clamp");
        var page = await ReadAsync(await executive.CreateClient().GetAsync(DecisionsUrl + query));

        Assert.True(page.GetProperty("page").GetInt32() >= 1);
        Assert.True(page.GetProperty("pageSize").GetInt32() >= 1);
    }

    [Fact]
    public async Task The_log_offers_only_the_actions_the_caller_may_actually_take()
    {
        await SeedRecommendationAsync();

        using var reviewer = Analyst("analyst-actions");
        var asAnalyst = await ReadAsync(await reviewer.CreateClient().GetAsync($"{DecisionsUrl}?status=Generated"));

        foreach (var item in asAnalyst.GetProperty("items").EnumerateArray())
        {
            var actions = item.GetProperty("availableActions").EnumerateArray().Select(a => a.GetString()).ToList();

            Assert.Contains("claim", actions);
            Assert.DoesNotContain("accept", actions);
        }
    }

    // ---------------------------------------------------------------- the detail view

    [Fact]
    public async Task The_detail_shows_the_frozen_original_beside_the_human_decision()
    {
        var (recommendationId, decisionId) = await ClaimedAsync("analyst-detail");

        using var approver = Executive("exec-detail");
        await approver.CreateClient().SendAsync(Post(
            $"{DecisionsUrl}/{decisionId}/accept-with-modification",
            new { field = "proposed_qty", from = 40m, to = 30m, rationale = "Capacity" },
            NewKey()));

        var detail = await ReadAsync(await approver.CreateClient().GetAsync($"{DecisionsUrl}/{recommendationId}"));

        var recommendation = detail.GetProperty("recommendation");
        Assert.False(string.IsNullOrWhiteSpace(recommendation.GetProperty("rationale").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(recommendation.GetProperty("rulesetVersion").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(recommendation.GetProperty("datasetVersion").GetString()));
        Assert.NotEmpty(recommendation.GetProperty("evidence").EnumerateArray());

        var decision = detail.GetProperty("decision");
        Assert.Equal("AcceptedModified", decision.GetProperty("outcome").GetString());
        Assert.Equal("exec-detail", decision.GetProperty("decidedBy").GetString());
        Assert.Equal(30m, decision.GetProperty("modification").GetProperty("to").GetDecimal());
        Assert.Equal(40m, decision.GetProperty("modification").GetProperty("from").GetDecimal());

        // The full trail, in order.
        var events = detail.GetProperty("statusEvents").EnumerateArray()
            .Select(e => e.GetProperty("toStatus").GetString()).ToList();

        Assert.Equal(["Generated", "UnderReview", "AcceptedModified"], events);
        Assert.Single(detail.GetProperty("approvalSteps").EnumerateArray());
    }

    [Fact]
    public async Task An_unclaimed_record_still_appears_in_the_detail_view_with_no_decision()
    {
        var recommendationId = await SeedRecommendationAsync();

        using var executive = Executive("exec-nodecision");
        var detail = await ReadAsync(await executive.CreateClient().GetAsync($"{DecisionsUrl}/{recommendationId}"));

        Assert.Equal(JsonValueKind.Null, detail.GetProperty("decision").ValueKind);
        Assert.Empty(detail.GetProperty("approvalSteps").EnumerateArray());
        Assert.Single(detail.GetProperty("statusEvents").EnumerateArray());
    }

    // ---------------------------------------------------------------- outcomes

    [Fact]
    public async Task The_outcome_recorder_may_be_the_person_who_decided()
    {
        // Deliberate: measuring a realised result is observation, not a second approval. Requiring a
        // third party would simply mean outcomes never get recorded.
        var (_, decisionId) = await ClaimedAsync("analyst-recorder");

        using var approver = Executive("exec-recorder");
        var client = approver.CreateClient();

        await client.SendAsync(Post($"{DecisionsUrl}/{decisionId}/accept", null, NewKey()));
        await client.SendAsync(Post($"{DecisionsUrl}/{decisionId}/implemented", null, NewKey()));

        var response = await client.SendAsync(Post(
            $"{DecisionsUrl}/{decisionId}/outcome",
            new { metric = "Holding cost avoided", realisedValue = 1_234.56m, unit = "SAR", note = (string?)null },
            NewKey()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Only_one_outcome_may_be_recorded_per_decision()
    {
        var (_, decisionId) = await ClaimedAsync("analyst-one-outcome");

        using var approver = Executive("exec-one-outcome");
        var client = approver.CreateClient();

        await client.SendAsync(Post($"{DecisionsUrl}/{decisionId}/accept", null, NewKey()));
        await client.SendAsync(Post($"{DecisionsUrl}/{decisionId}/implemented", null, NewKey()));

        var body = new { metric = "Units sold", realisedValue = 4m, unit = "units", note = (string?)null };
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Post($"{DecisionsUrl}/{decisionId}/outcome", body, NewKey()))).StatusCode);

        var second = await client.SendAsync(Post($"{DecisionsUrl}/{decisionId}/outcome", body, NewKey()));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task A_realised_value_keeps_its_decimal_precision_through_the_round_trip()
    {
        var (_, decisionId) = await ClaimedAsync("analyst-precision");

        using var approver = Executive("exec-precision");
        var client = approver.CreateClient();

        await client.SendAsync(Post($"{DecisionsUrl}/{decisionId}/accept", null, NewKey()));
        await client.SendAsync(Post($"{DecisionsUrl}/{decisionId}/implemented", null, NewKey()));
        await client.SendAsync(Post(
            $"{DecisionsUrl}/{decisionId}/outcome",
            new { metric = "Holding cost avoided", realisedValue = 18_615.55m, unit = "SAR", note = (string?)null },
            NewKey()));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        var outcome = await db.ActionOutcomes.AsNoTracking().SingleAsync(o => o.DecisionId == decisionId);
        Assert.Equal(18_615.55m, outcome.RealisedValue);
    }

    [Fact]
    public async Task The_implemented_response_states_that_nothing_was_written_to_oracle_fusion()
    {
        var (_, decisionId) = await ClaimedAsync("analyst-fusion");

        using var approver = Executive("exec-fusion");
        var client = approver.CreateClient();

        await client.SendAsync(Post($"{DecisionsUrl}/{decisionId}/accept", null, NewKey()));
        var body = await ReadAsync(await client.SendAsync(
            Post($"{DecisionsUrl}/{decisionId}/implemented", null, NewKey())));

        Assert.Contains("Oracle Fusion", body.GetProperty("message").GetString()!, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- schema

    [Fact]
    public async Task The_migration_created_the_four_new_tables_and_left_the_eleven_existing_ones_alone()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        var tables = await db.Database
            .SqlQuery<string>($"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync();

        foreach (var added in new[] { "management_decisions", "approval_steps", "action_outcomes", "idempotency_records" })
        {
            Assert.Contains(added, tables);
        }

        // The eleven tables that existed before S6, still present and still named the same.
        foreach (var existing in new[]
                 {
                     "sales_facts", "inventory_items", "ingestion_batches", "vehicle_sales", "service_events",
                     "parts", "part_compatibilities", "part_supersessions", "part_usages",
                     "recommendations", "recommendation_status_events",
                 })
        {
            Assert.Contains(existing, tables);
        }
    }

    [Fact]
    public async Task The_identity_endpoint_reports_the_caller_and_never_anyone_else()
    {
        using var executive = Executive("exec-identity");
        var me = await ReadAsync(await executive.CreateClient().GetAsync("/api/v1/identity/me"));

        Assert.True(me.GetProperty("isAuthenticated").GetBoolean());
        Assert.Equal("exec-identity", me.GetProperty("subjectId").GetString());

        var permissions = me.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ToList();
        Assert.Contains(Permissions.RecommendationApprove, permissions);
        Assert.DoesNotContain(Permissions.RecommendationGenerate, permissions);
    }

    [Fact]
    public async Task The_identity_endpoint_renders_a_signed_out_state_rather_than_failing()
    {
        using var secured = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Provider)}", nameof(AuthProvider.EntraId));
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Authority)}", "https://login.microsoftonline.com/t/v2.0");
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Audience)}", "api://beeeye-test");
        });

        var me = await ReadAsync(await secured.CreateClient().GetAsync("/api/v1/identity/me"));

        Assert.False(me.GetProperty("isAuthenticated").GetBoolean());
        Assert.Empty(me.GetProperty("permissions").EnumerateArray());
        Assert.Empty(me.GetProperty("roles").EnumerateArray());
    }
}
