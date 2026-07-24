using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BeeEye.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeeEye.IntegrationTests;

/// <summary>
/// End-to-end tests for the Data Health screen (V3-GOV-008). They run against the real composition root
/// over the seeded sample dataset, so they prove the module reports its real row counts, derives an
/// honest per-source status, and adds neither a table nor a delete path.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class DataHealthApiTests(IntegrationTestFactory factory)
{
    private const string HealthUrl = "/api/v1/data-quality/health";
    private static readonly string[] ValidStatusKinds = ["ready", "assumptions", "demo", "blocked"];

    private async Task<JsonElement> HealthAsync()
    {
        var json = await factory.CreateClient().GetStringAsync(HealthUrl);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Fact]
    public async Task Module_reports_itself_as_operational()
    {
        using var doc = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/api/v1/data-quality"));
        Assert.Equal("operational", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Health_is_reachable_and_returns_ok()
    {
        var response = await factory.CreateClient().GetAsync(HealthUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Real_row_counts_match_the_seeded_dataset_profile()
    {
        var root = await HealthAsync();

        Assert.Equal(3120, root.GetProperty("salesRows").GetInt32());
        Assert.Equal(291, root.GetProperty("invRows").GetInt32());
        Assert.True(root.GetProperty("models").GetInt32() > 0);
        Assert.True(root.GetProperty("locations").GetInt32() > 0);
    }

    [Fact]
    public async Task Startup_seed_has_only_current_batches_so_health_counts_current_rows()
    {
        // Data Health counts SalesFact/InventoryItem rows directly; the importer guarantees those are the
        // *current* rows by deleting a superseded batch's rows at ingestion. This pins that guarantee at
        // the source: the fresh seed carries three batches and none is superseded, so the row counts
        // asserted in Real_row_counts_match_the_seeded_dataset_profile can never include stale,
        // double-counted rows from a re-seed.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        var total = await db.IngestionBatches.CountAsync();
        var completed = await db.IngestionBatches.CountAsync(b => b.Status == "completed");
        var superseded = await db.IngestionBatches.CountAsync(b => b.Status == "superseded");

        Assert.Equal(3, completed);
        Assert.Equal(total, completed); // every seeded batch is current
        Assert.Equal(0, superseded);
    }

    [Fact]
    public async Task Score_is_present_and_sits_in_its_declared_band()
    {
        var root = await HealthAsync();

        var score = root.GetProperty("score").GetInt32();
        Assert.InRange(score, 0, 100);

        var expected = score >= 85 ? "Healthy" : score >= 70 ? "Warning" : "Critical";
        Assert.Equal(expected, root.GetProperty("scoreBand").GetString());
    }

    [Fact]
    public async Task Coverage_reads_as_a_month_range_over_real_sales()
    {
        var coverage = (await HealthAsync()).GetProperty("coverage").GetString();

        Assert.False(string.IsNullOrWhiteSpace(coverage));
        Assert.NotEqual("—", coverage); // the seed has sales, so coverage is a real range
        Assert.Matches(@"\d{4}", coverage!); // contains a four-digit year
    }

    [Fact]
    public async Task There_are_seven_sources_with_valid_statuses()
    {
        var sources = (await HealthAsync()).GetProperty("sources").EnumerateArray().ToList();

        Assert.Equal(7, sources.Count);
        foreach (var s in sources)
        {
            Assert.False(string.IsNullOrWhiteSpace(s.GetProperty("name").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(s.GetProperty("status").GetString()));
            Assert.Contains(s.GetProperty("statusKind").GetString(), ValidStatusKinds);
        }

        // Exactly four demo sources and one blocked source, honestly labelled.
        Assert.Equal(4, sources.Count(s => s.GetProperty("statusKind").GetString() == "demo"));
        Assert.Single(sources, s => s.GetProperty("statusKind").GetString() == "blocked");
    }

    [Fact]
    public async Task Inventory_status_stays_in_lock_step_with_the_location_mismatch_issue()
    {
        var root = await HealthAsync();

        var locIssue = root.GetProperty("issues").EnumerateArray().Single(i => i.GetProperty("id").GetString() == "loc");
        var mismatchCount = locIssue.GetProperty("count").GetInt32();

        var inventory = root.GetProperty("sources").EnumerateArray()
            .Single(s => s.GetProperty("name").GetString() == "Inventory on-hand");
        var status = inventory.GetProperty("status").GetString();

        if (mismatchCount > 0)
        {
            Assert.Equal("Ready with assumptions", status);
        }
        else
        {
            Assert.Equal("Ready", status);
        }
    }

    [Fact]
    public async Task Issues_list_every_check_with_a_severity()
    {
        var issues = (await HealthAsync()).GetProperty("issues").EnumerateArray().ToList();

        string[] expectedIds = ["dup_stock", "dup_chassis", "rev", "lead", "neg", "dates", "loc"];
        Assert.Equal(expectedIds, issues.Select(i => i.GetProperty("id").GetString()).ToArray());
        Assert.All(issues, i => Assert.Contains(i.GetProperty("severity").GetString(), new[] { "ok", "medium", "high" }));
    }

    // ---------------------------------------------------------------- no delete / no new table

    [Fact]
    public async Task There_is_no_delete_route_under_data_quality()
    {
        var response = await factory.CreateClient().DeleteAsync(HealthUrl);
        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"DELETE {HealthUrl} returned {(int)response.StatusCode}; Data Health is read-only.");
    }

    [Fact]
    public async Task The_served_document_declares_no_delete_under_data_quality()
    {
        var document = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        foreach (var path in document.GetProperty("paths").EnumerateObject())
        {
            if (!path.Name.StartsWith("/api/v1/data-quality", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.False(path.Value.TryGetProperty("delete", out _), $"{path.Name} declares a DELETE.");
        }
    }

    [Fact]
    public async Task S7_added_no_database_table()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();

        var tables = await db.Database
            .SqlQuery<string>($"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync();

        // The sixteen domain tables that existed before S7 (S7 is read-only) — still present, and no more.
        string[] expected =
        [
            "sales_facts", "inventory_items", "ingestion_batches", "vehicle_sales", "service_events",
            "parts", "part_compatibilities", "part_supersessions", "part_usages",
            "recommendations", "recommendation_status_events",
            "management_decisions", "approval_steps", "action_outcomes", "idempotency_records",
            "explainability_feedback",
        ];

        foreach (var table in expected)
        {
            Assert.Contains(table, tables);
        }

        var domainTables = tables.Where(t => !t.StartsWith("__", StringComparison.Ordinal)).ToList();
        Assert.Equal(expected.Length, domainTables.Count); // S7 introduced no new table
    }

    [Fact]
    public async Task Health_responds_within_a_reasonable_budget()
    {
        var client = factory.CreateClient();
        await client.GetStringAsync(HealthUrl); // warm

        var sw = Stopwatch.StartNew();
        await client.GetStringAsync(HealthUrl);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 5_000, $"data health took {sw.ElapsedMilliseconds}ms");
    }
}
