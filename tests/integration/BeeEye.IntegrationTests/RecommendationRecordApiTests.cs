using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BeeEye.Persistence;
using BeeEye.Shared.Decisions;
using BeeEye.Shared.Security;
using BeeEye.Shared.Web.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeeEye.IntegrationTests;

/// <summary>
/// End-to-end tests for the platform's first write path: persisting engine recommendations as frozen,
/// append-only records (<c>docs/adr/0006-recommendation-decision-workflow.md</c>).
/// <para>
/// These assert the three properties the ADR exists to guarantee — the original is immutable, status
/// lives in an append-only log, and generation is idempotent — plus the authorization rules from
/// ADR 0008 that make the record accountable.
/// </para>
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class RecommendationRecordApiTests(IntegrationTestFactory factory)
{
    private const string GenerateUrl = "/api/v1/recommendations/records/generate";
    private const string RecordsUrl = "/api/v1/recommendations/records";

    /// <summary>Re-hosts with authorization enforced and the caller holding the given roles.</summary>
    private WebApplicationFactory<Program> As(params string[] roles) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.RequireAuthenticatedReads)}", "true");
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Provider)}", nameof(AuthProvider.LocalDev));
            builder.UseSetting("Auth:LocalDevUser:Roles", string.Empty);
            for (var i = 0; i < roles.Length; i++)
            {
                builder.UseSetting($"Auth:LocalDevUser:Roles:{i}", roles[i]);
            }
        });

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    /// <summary>Generates as an Analyst — the authoring role — and returns the run summary.</summary>
    private async Task<JsonElement> GenerateAsync()
    {
        using var analyst = As(PlatformRoles.Analyst);
        return await ReadAsync(await analyst.CreateClient().PostAsync(GenerateUrl, null));
    }

    // ---------------------------------------------------------------- authorization

    [Fact]
    public async Task Generation_requires_authentication_even_in_the_relaxed_read_posture()
    {
        // The unmodified Development factory relaxes *reads*. A write must still be refused, because
        // no configuration setting may open a state-changing path (ADR 0008 §2.4).
        using var secured = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Provider)}", nameof(AuthProvider.EntraId));
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Authority)}", "https://login.microsoftonline.com/t/v2.0");
            builder.UseSetting($"{AuthOptions.SectionName}:{nameof(AuthOptions.Audience)}", "api://beeeye-test");
        });

        var response = await secured.CreateClient().PostAsync(GenerateUrl, null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_executive_cannot_generate_recommendations()
    {
        // Segregation of duties: the approver must not also be the author.
        using var executive = As(PlatformRoles.Executive);
        var response = await executive.CreateClient().PostAsync(GenerateUrl, null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_it_admin_cannot_generate_recommendations()
    {
        using var admin = As(PlatformRoles.ItAdmin);
        var response = await admin.CreateClient().PostAsync(GenerateUrl, null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_analyst_may_generate_recommendations()
    {
        using var analyst = As(PlatformRoles.Analyst);
        var response = await analyst.CreateClient().PostAsync(GenerateUrl, null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Reading_records_requires_the_review_permission()
    {
        using var admin = As(PlatformRoles.ItAdmin);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.CreateClient().GetAsync(RecordsUrl)).StatusCode);

        using var executive = As(PlatformRoles.Executive);
        Assert.Equal(HttpStatusCode.OK, (await executive.CreateClient().GetAsync(RecordsUrl)).StatusCode);
    }

    // ---------------------------------------------------------------- idempotency

    [Fact]
    public async Task Generation_is_idempotent_across_repeated_runs()
    {
        await GenerateAsync();

        // Whatever the first run created, a second must create nothing more.
        var second = await GenerateAsync();
        var third = await GenerateAsync();

        Assert.Equal(0, second.GetProperty("created").GetInt32());
        Assert.Equal(0, third.GetProperty("created").GetInt32());
        Assert.True(second.GetProperty("alreadyPresent").GetInt32() > 0);
    }

    [Fact]
    public async Task Repeated_generation_does_not_duplicate_records()
    {
        await GenerateAsync();
        var before = await CountRecordsAsync();

        await GenerateAsync();
        await GenerateAsync();

        Assert.Equal(before, await CountRecordsAsync());
    }

    [Fact]
    public async Task Concurrent_generation_runs_do_not_duplicate_records()
    {
        await GenerateAsync();
        var before = await CountRecordsAsync();

        using var analyst = As(PlatformRoles.Analyst);
        var client = analyst.CreateClient();

        // Fire several at once: the unique index, not the existence check, is the real guarantee.
        var responses = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => client.PostAsync(GenerateUrl, null)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(before, await CountRecordsAsync());
    }

    [Fact]
    public async Task Idempotency_keys_are_unique_across_all_records()
    {
        await GenerateAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();
        var keys = await db.Recommendations.AsNoTracking().Select(r => r.IdempotencyKey).ToListAsync();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }

    // ---------------------------------------------------------------- the frozen record

    [Fact]
    public async Task A_generated_record_carries_its_full_provenance()
    {
        await GenerateAsync();

        using var executive = As(PlatformRoles.Executive);
        var page = await ReadAsync(await executive.CreateClient().GetAsync(RecordsUrl));
        var items = page.GetProperty("items").EnumerateArray().ToList();

        Assert.NotEmpty(items);

        foreach (var item in items)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("rulesetVersion").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("datasetVersion").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("analysisDate").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("ruleId").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("action").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("rationale").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("ownerRole").GetString()));
            Assert.NotEmpty(item.GetProperty("evidence").EnumerateArray());
        }
    }

    [Fact]
    public async Task Every_new_record_starts_in_the_generated_state()
    {
        await GenerateAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        // Scoped to records nobody has claimed. From S6 the same database also carries records that
        // have deliberately been moved on through the decision workflow, so "every record is
        // Generated" is no longer the claim — "an untouched record is Generated" is.
        var untouched = await db.Recommendations
            .AsNoTracking()
            .Where(r => !db.ManagementDecisions.Any(d => d.RecommendationId == r.Id))
            .ToListAsync();

        Assert.NotEmpty(untouched);
        Assert.All(untouched, r => Assert.Equal(RecommendationStatus.Generated, r.CurrentStatus));
    }

    [Fact]
    public async Task A_synthetic_recommendation_records_its_demo_provenance_as_an_assumption()
    {
        await GenerateAsync();

        using var executive = As(PlatformRoles.Executive);
        var page = await ReadAsync(await executive.CreateClient().GetAsync(RecordsUrl));

        foreach (var item in page.GetProperty("items").EnumerateArray())
        {
            if (!item.GetProperty("isDemoData").GetBoolean())
            {
                continue;
            }

            var assumptions = item.GetProperty("assumptions").EnumerateArray()
                .Select(a => a.GetString() ?? string.Empty).ToList();

            Assert.Contains(assumptions, a => a.Contains("synthetic", StringComparison.OrdinalIgnoreCase));
        }
    }

    // ---------------------------------------------------------------- the append-only log

    [Fact]
    public async Task Every_record_has_an_opening_status_event_attributed_to_the_system()
    {
        await GenerateAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        var records = await db.Recommendations.AsNoTracking().Include(r => r.StatusEvents).ToListAsync();
        Assert.NotEmpty(records);

        foreach (var record in records)
        {
            var opening = Assert.Single(record.StatusEvents, e => e.FromStatus == null);

            Assert.Equal(RecommendationStatus.Generated, opening.ToStatus);
            // The engine acted, not a person. Attributing generation to a human would corrupt the
            // accountability trail the ADR exists to protect.
            Assert.Equal("system", opening.Actor);
            Assert.NotEqual(default, opening.AtUtc);
        }
    }

    [Fact]
    public async Task The_projected_status_agrees_with_the_last_event_in_the_log()
    {
        await GenerateAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        foreach (var record in await db.Recommendations.AsNoTracking().Include(r => r.StatusEvents).ToListAsync())
        {
            var latest = record.StatusEvents.OrderByDescending(e => e.AtUtc).First();
            Assert.Equal(latest.ToStatus, record.CurrentStatus);
        }
    }

    [Fact]
    public async Task A_record_carries_a_validity_window_so_it_can_expire_unreviewed()
    {
        await GenerateAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        foreach (var record in await db.Recommendations.AsNoTracking().ToListAsync())
        {
            Assert.NotNull(record.ValidUntilUtc);
            Assert.True(record.ValidUntilUtc > record.CreatedAtUtc);
        }
    }

    // ---------------------------------------------------------------- reading

    [Fact]
    public async Task Records_are_returned_highest_priority_first()
    {
        await GenerateAsync();

        using var executive = As(PlatformRoles.Executive);
        var page = await ReadAsync(await executive.CreateClient().GetAsync(RecordsUrl));

        var priorities = page.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("priority").GetInt32()).ToList();

        Assert.True(priorities.SequenceEqual(priorities.OrderByDescending(p => p)));
    }

    [Fact]
    public async Task Records_can_be_filtered_by_status()
    {
        await GenerateAsync();

        using var executive = As(PlatformRoles.Executive);
        var client = executive.CreateClient();

        var generated = await ReadAsync(await client.GetAsync($"{RecordsUrl}?status=Generated"));
        Assert.NotEmpty(generated.GetProperty("items").EnumerateArray());

        // Asserted as "every row carries the requested status" rather than "no row is rejected":
        // from S6 the workflow genuinely produces rejected records, and a filter test that depended on
        // their absence was testing the fixture, not the filter.
        foreach (var status in new[] { "Generated", "Rejected", "Implemented" })
        {
            var page = await ReadAsync(await client.GetAsync($"{RecordsUrl}?status={status}"));

            Assert.All(
                page.GetProperty("items").EnumerateArray(),
                item => Assert.Equal(status, item.GetProperty("currentStatus").GetString()));
        }
    }

    [Fact]
    public async Task An_unknown_status_is_a_client_error_with_the_valid_values()
    {
        using var executive = As(PlatformRoles.Executive);
        var response = await executive.CreateClient().GetAsync($"{RecordsUrl}?status=Banana");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Generated", problem.GetProperty("detail").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Page_size_is_clamped_so_an_unbounded_set_never_reaches_the_browser()
    {
        await GenerateAsync();

        using var executive = As(PlatformRoles.Executive);
        var page = await ReadAsync(await executive.CreateClient().GetAsync($"{RecordsUrl}?pageSize=100000"));

        Assert.True(page.GetProperty("pageSize").GetInt32() <= 200);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?page=-5")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=-1")]
    public async Task Out_of_range_paging_is_clamped_rather_than_erroring(string query)
    {
        using var executive = As(PlatformRoles.Executive);
        var response = await executive.CreateClient().GetAsync(RecordsUrl + query);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(page.GetProperty("page").GetInt32() >= 1);
        Assert.True(page.GetProperty("pageSize").GetInt32() >= 1);
    }

    private async Task<int> CountRecordsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();
        return await db.Recommendations.AsNoTracking().CountAsync();
    }
}
