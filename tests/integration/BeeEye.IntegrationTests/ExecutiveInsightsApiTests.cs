using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Xunit;

namespace BeeEye.IntegrationTests;

/// <summary>
/// End-to-end tests for the UC8 Executive Decision Cockpit feed. These run against the real
/// composition root over the seeded sample dataset, so they prove the cross-context
/// <c>IDecisionSignalProvider</c> seam actually resolves and produces decisions.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ExecutiveInsightsApiTests(IntegrationTestFactory factory)
{
    private const string FeedUrl = "/api/v1/executive-insights/decision-feed";

    private static readonly string[] Severities = ["Low", "Medium", "High"];
    private static readonly string[] Kinds = ["Risk", "Opportunity"];
    private static readonly string[] ConfidenceBands = ["Low", "Medium", "High"];

    private async Task<JsonElement> FeedAsync()
    {
        var client = factory.CreateClient();
        var json = await client.GetStringAsync(FeedUrl);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Fact]
    public async Task Feed_is_reachable_and_returns_ok()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync(FeedUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Module_reports_itself_as_operational()
    {
        var client = factory.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/v1/executive-insights"));

        Assert.Equal("operational", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Feed_produces_decisions_from_the_seeded_dataset()
    {
        var root = await FeedAsync();
        var decisions = root.GetProperty("decisions").EnumerateArray().ToList();

        Assert.NotEmpty(decisions);
    }

    [Fact]
    public async Task Every_provider_contributes_without_a_gap()
    {
        var root = await FeedAsync();
        var gaps = root.GetProperty("gaps").EnumerateArray()
            .Select(g => g.GetProperty("area").GetString())
            .ToList();

        Assert.True(gaps.Count == 0, $"Providers failed: {string.Join(", ", gaps)}");
    }

    [Fact]
    public async Task Decisions_are_ranked_by_descending_priority()
    {
        var root = await FeedAsync();
        var priorities = root.GetProperty("decisions").EnumerateArray()
            .Select(d => d.GetProperty("priority").GetInt32())
            .ToList();

        Assert.True(
            priorities.SequenceEqual(priorities.OrderByDescending(p => p)),
            $"priorities must be descending, got [{string.Join(", ", priorities)}]");
    }

    [Fact]
    public async Task Every_decision_carries_a_complete_explainable_payload()
    {
        var root = await FeedAsync();

        foreach (var d in root.GetProperty("decisions").EnumerateArray())
        {
            var id = d.GetProperty("id").GetString();

            Assert.False(string.IsNullOrWhiteSpace(id));
            Assert.False(string.IsNullOrWhiteSpace(d.GetProperty("title").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(d.GetProperty("area").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(d.GetProperty("screen").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(d.GetProperty("whyNow").GetString()), $"{id} whyNow");
            Assert.False(string.IsNullOrWhiteSpace(d.GetProperty("action").GetString()), $"{id} action");
            Assert.False(string.IsNullOrWhiteSpace(d.GetProperty("evidence").GetString()), $"{id} evidence");
            Assert.False(string.IsNullOrWhiteSpace(d.GetProperty("ownerRole").GetString()), $"{id} ownerRole");

            var priority = d.GetProperty("priority").GetInt32();
            Assert.InRange(priority, 0, 100);

            Assert.Contains(d.GetProperty("severity").GetString(), Severities);
            Assert.Contains(d.GetProperty("kind").GetString(), Kinds);
            Assert.Contains(d.GetProperty("confidence").GetString(), ConfidenceBands);
            Assert.InRange(d.GetProperty("confidencePct").GetInt32(), 0, 100);
            Assert.InRange(d.GetProperty("dueDays").GetInt32(), 1, 60);

            // Four ranked drivers, each a whole percentage.
            var factors = d.GetProperty("factors").EnumerateArray().ToList();
            Assert.Equal(4, factors.Count);
            foreach (var f in factors)
            {
                Assert.InRange(f.GetProperty("percent").GetInt32(), 0, 100);
            }
        }
    }

    [Fact]
    public async Task Every_decision_links_to_a_screen_the_web_app_actually_routes()
    {
        // Keep the cockpit's drill-down honest: a decision must not point at a screen that does not
        // exist. These ids mirror src/web/src/config/navigation.ts.
        string[] routableScreens =
        [
            "executive-cockpit", "order-optimisation", "sales-forecasting", "configuration-demand",
            "procurement", "inventory-intelligence", "after-sales", "spare-parts",
            "data-management", "platform-settings",
        ];

        var root = await FeedAsync();

        foreach (var d in root.GetProperty("decisions").EnumerateArray())
        {
            var screen = d.GetProperty("screen").GetString();
            Assert.Contains(screen, routableScreens);
        }
    }

    [Fact]
    public async Task Decision_ids_are_unique()
    {
        var root = await FeedAsync();
        var ids = root.GetProperty("decisions").EnumerateArray()
            .Select(d => d.GetProperty("id").GetString())
            .ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Summary_agrees_with_the_decisions_it_summarises()
    {
        var root = await FeedAsync();
        var decisions = root.GetProperty("decisions").EnumerateArray().ToList();
        var summary = root.GetProperty("summary");

        Assert.Equal(decisions.Count, summary.GetProperty("total").GetInt32());

        var critical = decisions.Count(d => d.GetProperty("severity").GetString() == "High");
        Assert.Equal(critical, summary.GetProperty("critical").GetInt32());

        var dueThisWeek = decisions.Count(d => d.GetProperty("dueDays").GetInt32() <= 7);
        Assert.Equal(dueThisWeek, summary.GetProperty("dueThisWeek").GetInt32());

        var demo = decisions.Count(d => d.GetProperty("isDemo").GetBoolean());
        Assert.Equal(demo, summary.GetProperty("demoDataCount").GetInt32());
    }

    [Fact]
    public async Task Synthetic_after_sales_and_parts_decisions_are_flagged_as_demo_data()
    {
        var root = await FeedAsync();

        foreach (var d in root.GetProperty("decisions").EnumerateArray())
        {
            var area = d.GetProperty("area").GetString();
            if (area is "Parts" or "After-Sales")
            {
                Assert.True(
                    d.GetProperty("isDemo").GetBoolean(),
                    $"{d.GetProperty("id").GetString()} derives from synthetic data and must be labelled");
            }
        }
    }

    [Fact]
    public async Task Narrative_is_present_and_consistent_with_the_decision_count()
    {
        var root = await FeedAsync();
        var narrative = root.GetProperty("narrative").GetString();
        var total = root.GetProperty("summary").GetProperty("total").GetInt32();

        Assert.False(string.IsNullOrWhiteSpace(narrative));

        if (total == 0)
        {
            Assert.Contains("No material exceptions", narrative, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains(total.ToString(), narrative, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Feed_is_deterministic_across_repeated_requests()
    {
        var first = await FeedAsync();
        var second = await FeedAsync();

        var firstIds = first.GetProperty("decisions").EnumerateArray()
            .Select(d => d.GetProperty("id").GetString()).ToList();
        var secondIds = second.GetProperty("decisions").EnumerateArray()
            .Select(d => d.GetProperty("id").GetString()).ToList();

        Assert.Equal(firstIds, secondIds);
    }

    [Fact]
    public async Task Feed_responds_within_the_cockpit_budget()
    {
        var client = factory.CreateClient();

        // Warm the caches so this measures steady-state, not first-request compilation.
        await client.GetStringAsync(FeedUrl);

        var sw = Stopwatch.StartNew();
        await client.GetStringAsync(FeedUrl);
        sw.Stop();

        // The cockpit aggregates six contexts, including the two slowest endpoints recorded in
        // docs/implementation/v3-baseline.md (669ms and 275ms warm). This guards against a
        // regression that would make the landing screen unusable; see risk R-06.
        Assert.True(
            sw.ElapsedMilliseconds < 5_000,
            $"decision feed took {sw.ElapsedMilliseconds}ms, exceeding the 5000ms budget");
    }
}
