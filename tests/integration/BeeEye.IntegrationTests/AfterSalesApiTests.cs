using System.Diagnostics;
using System.Net;
using System.Text.Json;
using BeeEye.Persistence.Caching;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeeEye.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class AfterSalesApiTests(IntegrationTestFactory factory)
{
    private const string SummaryUrl = "/api/v1/after-sales/service-intensity/summary";

    [Fact]
    public async Task Summary_returns_synthetic_provenance_and_fleet_metrics()
    {
        var client = factory.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/v1/after-sales/service-intensity/summary"));
        var root = doc.RootElement;

        Assert.Equal("synthetic-demo", root.GetProperty("meta").GetProperty("provenance").GetString());
        var summary = root.GetProperty("summary");
        Assert.True(summary.GetProperty("modelsTracked").GetInt32() > 0);
        Assert.True(summary.GetProperty("totalEvents").GetInt32() > 0);
        Assert.True(summary.GetProperty("fleetEventsPerVehicle").GetDouble() > 0);
    }

    [Fact]
    public async Task ByModel_is_paged_and_ordered_by_intensity()
    {
        var client = factory.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/v1/after-sales/service-intensity/by-model?pageSize=3"));
        var page = doc.RootElement.GetProperty("page");
        var items = page.GetProperty("items").EnumerateArray().ToList();

        Assert.NotEmpty(items);
        Assert.Equal("synthetic-demo", doc.RootElement.GetProperty("meta").GetProperty("provenance").GetString());

        // Non-null intensity indices are in non-increasing order (default sort).
        double? prev = null;
        foreach (var m in items)
        {
            if (m.GetProperty("intensityIndex").ValueKind == JsonValueKind.Number)
            {
                var idx = m.GetProperty("intensityIndex").GetDouble();
                if (prev is not null)
                {
                    Assert.True(idx <= prev.Value + 1e-9);
                }

                prev = idx;
            }
        }
    }

    [Fact]
    public async Task Detail_keeps_service_types_separate_and_reports_coverage()
    {
        var client = factory.CreateClient();
        using var list = JsonDocument.Parse(await client.GetStringAsync("/api/v1/after-sales/service-intensity/by-model?pageSize=1"));
        var model = list.RootElement.GetProperty("page").GetProperty("items")[0].GetProperty("model").GetString()!;

        using var doc = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/after-sales/service-intensity/model/{Uri.EscapeDataString(model)}"));
        var detail = doc.RootElement.GetProperty("model");

        var types = detail.GetProperty("byServiceType").EnumerateArray().Select(t => t.GetProperty("serviceType").GetString()).ToList();
        Assert.Equal(new[] { "Routine", "Repair", "Warranty", "Recall" }, types);

        var coverage = detail.GetProperty("coverage");
        Assert.Contains(coverage.GetProperty("reliabilityTier").GetString(), new[] { "High", "Medium", "Low" });
        Assert.True(detail.GetProperty("byMileageBand").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Unknown_model_returns_problem_details_404()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/after-sales/service-intensity/model/DoesNotExist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.ToString());
    }

    // ---- V3-PERF-001: the data-versioned analysis cache -----------------------------------------

    [Fact]
    public async Task A_warm_summary_recomputes_nothing()
    {
        var client = factory.CreateClient();
        var cache = factory.Services.GetRequiredService<DataVersionedCache>();

        // Warm the entry first so the state is deterministic regardless of what other tests ran.
        await client.GetStringAsync(SummaryUrl);

        var before = cache.ComputeCount;
        await client.GetStringAsync(SummaryUrl);

        // The deterministic cache-hit proof: a second identical request runs no expensive compute.
        Assert.Equal(before, cache.ComputeCount);
    }

    [Fact]
    public async Task All_UC6_endpoints_are_served_from_one_cached_analysis()
    {
        var client = factory.CreateClient();
        var cache = factory.Services.GetRequiredService<DataVersionedCache>();

        // Warm the single shared analysis entry.
        await client.GetStringAsync(SummaryUrl);
        var before = cache.ComputeCount;

        using var byModel = JsonDocument.Parse(
            await client.GetStringAsync("/api/v1/after-sales/service-intensity/by-model?pageSize=1"));
        var model = byModel.RootElement.GetProperty("page").GetProperty("items")[0].GetProperty("model").GetString()!;
        await client.GetStringAsync($"/api/v1/after-sales/service-intensity/model/{Uri.EscapeDataString(model)}");
        await client.GetStringAsync(SummaryUrl);

        // /summary, /by-model and /model/{model} each take their slice of the one cached analysis.
        Assert.Equal(before, cache.ComputeCount);
    }

    [Fact]
    public async Task Summary_is_byte_identical_across_repeated_requests()
    {
        var client = factory.CreateClient();
        var first = await client.GetStringAsync(SummaryUrl);
        var second = await client.GetStringAsync(SummaryUrl);

        // The envelope's provenance carries a per-request UTC timestamp; the analysed summary must not move.
        Assert.Equal(SummaryOf(first), SummaryOf(second));
    }

    [Fact]
    public async Task Warm_summary_stays_well_within_the_latency_budget()
    {
        var client = factory.CreateClient();
        await client.GetStringAsync(SummaryUrl); // warm

        var sw = Stopwatch.StartNew();
        await client.GetStringAsync(SummaryUrl);
        sw.Stop();

        // Pre-cache warm baseline: 669 ms (docs/implementation/v3-baseline.md §4.4). This is a loose
        // regression net only — the compute-count proof above is the real assertion that the cache works.
        Assert.True(sw.ElapsedMilliseconds < 2_000, $"warm summary took {sw.ElapsedMilliseconds}ms");
    }

    private static string SummaryOf(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("summary").GetRawText();
    }
}
