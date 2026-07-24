using System.Diagnostics;
using System.Net;
using System.Text.Json;
using BeeEye.Persistence.Caching;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeeEye.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class SparePartsApiTests(IntegrationTestFactory factory)
{
    private const string SummaryUrl = "/api/v1/spare-parts/demand/summary";
    [Fact]
    public async Task Summary_returns_synthetic_provenance_and_class_mix()
    {
        var client = factory.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/v1/spare-parts/demand/summary"));
        var root = doc.RootElement;

        Assert.Equal("synthetic-demo", root.GetProperty("meta").GetProperty("provenance").GetString());
        var summary = root.GetProperty("summary");
        Assert.True(summary.GetProperty("distinctParts").GetInt32() > 0);
        Assert.True(summary.GetProperty("stockingPoints").GetInt32() > 0);
        Assert.True(summary.GetProperty("byDemandClass").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Parts_table_is_paged_and_exercises_intermittent_methods()
    {
        var client = factory.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/v1/spare-parts/demand/parts?pageSize=100&sort=part"));
        var items = doc.RootElement.GetProperty("page").GetProperty("items").EnumerateArray().ToList();

        Assert.NotEmpty(items);
        Assert.Equal("synthetic-demo", doc.RootElement.GetProperty("meta").GetProperty("provenance").GetString());
        foreach (var r in items)
        {
            Assert.False(string.IsNullOrEmpty(r.GetProperty("location").GetString()));
            Assert.Contains(r.GetProperty("stockoutRisk").GetString(), new[] { "Low", "Medium", "High", "Unknown" });
        }

        // Intermittent demand means Croston-family methods (SBA/TSB/Croston) are chosen for some part×location.
        var methods = items.Select(r => r.GetProperty("method").GetString()).ToHashSet();
        Assert.True(methods.Overlaps(new[] { "SBA", "TSB", "Croston" }));
    }

    [Fact]
    public async Task Low_data_parts_are_flagged_without_a_fabricated_forecast()
    {
        var client = factory.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/v1/spare-parts/demand/parts?lowDataOnly=true&pageSize=200"));
        var items = doc.RootElement.GetProperty("page").GetProperty("items").EnumerateArray().ToList();

        Assert.NotEmpty(items);
        foreach (var r in items)
        {
            Assert.True(r.GetProperty("insufficientData").GetBoolean());
            Assert.Equal(JsonValueKind.Null, r.GetProperty("predictedMonthlyDemand").ValueKind);
            Assert.Equal(JsonValueKind.Null, r.GetProperty("stockingRangeLow").ValueKind);
        }
    }

    [Fact]
    public async Task Part_detail_exposes_method_comparison_and_supersession_rollup()
    {
        var client = factory.CreateClient();
        // FLT-OIL-CO is a supersession successor in the synthetic catalogue.
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/v1/spare-parts/demand/part/FLT-OIL-CO"));
        var root = doc.RootElement;

        var comparison = root.GetProperty("national").GetProperty("comparison");
        foreach (var m in new[] { "ses", "croston", "sba", "tsb" })
        {
            Assert.Equal(JsonValueKind.Number, comparison.GetProperty(m).ValueKind);
        }

        Assert.True(root.GetProperty("byLocation").GetArrayLength() > 0);
        Assert.True(root.GetProperty("usageHistory").GetArrayLength() > 0);
        Assert.True(root.GetProperty("rolledUpSupersessions").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Higher_service_level_never_reduces_safety_stock()
    {
        var client = factory.CreateClient();
        static async Task<double> Safety(HttpClient c, double sl)
        {
            using var doc = JsonDocument.Parse(await c.GetStringAsync($"/api/v1/spare-parts/demand/part/BRK-PAD-F?serviceLevel={sl}"));
            return doc.RootElement.GetProperty("national").GetProperty("safetyStock").GetDouble();
        }

        Assert.True(await Safety(client, 0.99) > await Safety(client, 0.90));
    }

    [Fact]
    public async Task Unknown_part_returns_problem_details_404()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/spare-parts/demand/part/NOPE-404");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.ToString());
    }

    // ---- V3-PERF-001: the data-versioned, scenario-keyed summary cache --------------------------

    [Fact]
    public async Task Summary_is_cached_per_scenario_and_scenarios_never_cross_contaminate()
    {
        var client = factory.CreateClient();
        var cache = factory.Services.GetRequiredService<DataVersionedCache>();

        // Two non-default scenarios used only here, both on the (cached) summary endpoint.
        const string scenarioA = SummaryUrl + "?serviceLevel=0.9&reviewPeriodMonths=1";
        const string scenarioB = SummaryUrl + "?serviceLevel=0.99&reviewPeriodMonths=1";

        await client.GetStringAsync(scenarioA); // warm scenario A
        var before = cache.ComputeCount;

        await client.GetStringAsync(scenarioA); // same scenario -> cache hit, no recompute
        Assert.Equal(before, cache.ComputeCount);

        await client.GetStringAsync(scenarioB); // different scenario -> its own entry -> exactly one recompute
        Assert.Equal(before + 1, cache.ComputeCount);
    }

    [Fact]
    public async Task Summary_is_byte_identical_across_repeated_requests()
    {
        var client = factory.CreateClient();
        var first = await client.GetStringAsync(SummaryUrl);
        var second = await client.GetStringAsync(SummaryUrl);

        // The scenario + summary must be stable across requests; only the provenance timestamp moves.
        Assert.Equal(BodyWithoutMeta(first), BodyWithoutMeta(second));
    }

    [Fact]
    public async Task Warm_summary_stays_well_within_the_latency_budget()
    {
        var client = factory.CreateClient();
        await client.GetStringAsync(SummaryUrl); // warm

        var sw = Stopwatch.StartNew();
        await client.GetStringAsync(SummaryUrl);
        sw.Stop();

        // Pre-cache warm baseline: 275 ms (docs/implementation/v3-baseline.md §4.4). Loose regression net;
        // the compute-count proof above is the real assertion.
        Assert.True(sw.ElapsedMilliseconds < 2_000, $"warm summary took {sw.ElapsedMilliseconds}ms");
    }

    private static string BodyWithoutMeta(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return root.GetProperty("scenario").GetRawText() + "|" + root.GetProperty("summary").GetRawText();
    }
}
