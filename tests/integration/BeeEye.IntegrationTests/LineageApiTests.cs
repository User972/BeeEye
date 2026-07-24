using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace BeeEye.IntegrationTests;

/// <summary>
/// End-to-end tests for the Lineage screen (V3-GOV-009): the six-stage pipeline and the eight metrics
/// with their confirmed/demo provenance. The demo tags are cross-checked against the platform's
/// authoritative synthetic-demo labelling so the screen cannot drift from the rest of the app.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class LineageApiTests(IntegrationTestFactory factory)
{
    private const string LineageUrl = "/api/v1/models/lineage";

    private async Task<JsonElement> LineageAsync()
    {
        var json = await factory.CreateClient().GetStringAsync(LineageUrl);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Fact]
    public async Task Module_reports_itself_as_operational()
    {
        using var doc = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/api/v1/models"));
        Assert.Equal("operational", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Lineage_is_reachable_and_returns_ok()
    {
        var response = await factory.CreateClient().GetAsync(LineageUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_pipeline_has_six_ordered_stages_each_with_an_icon()
    {
        var stages = (await LineageAsync()).GetProperty("pipeline").EnumerateArray().ToList();

        Assert.Equal(6, stages.Count);
        Assert.Equal("Oracle Fusion ERP / CRM", stages[0].GetProperty("title").GetString());
        Assert.Equal("Decision Intelligence application", stages[5].GetProperty("title").GetString());
        Assert.All(stages, s => Assert.False(string.IsNullOrWhiteSpace(s.GetProperty("icon").GetString())));
    }

    [Fact]
    public async Task The_integration_stage_keeps_the_no_write_back_promise()
    {
        var stages = (await LineageAsync()).GetProperty("pipeline").EnumerateArray();
        var integration = stages.Single(s => s.GetProperty("kind").GetString() == "integration");

        Assert.Contains("no write-back", integration.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task There_are_eight_metrics_each_confirmed_or_demo()
    {
        var metrics = (await LineageAsync()).GetProperty("metrics").EnumerateArray().ToList();

        Assert.Equal(8, metrics.Count);
        Assert.All(metrics, m =>
        {
            Assert.False(string.IsNullOrWhiteSpace(m.GetProperty("source").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(m.GetProperty("basis").GetString()));
            Assert.Contains(m.GetProperty("state").GetString(), new[] { "confirmed", "demo" });
        });
    }

    [Fact]
    public async Task The_demo_metrics_agree_with_the_platforms_synthetic_demo_labelling()
    {
        // Lineage tags Procurement (UC4), Service-intensity (UC6) and Spare-parts (UC7) demo. For the two
        // the platform authoritatively flags — UC6/UC7 — bind to Decision.IsDemo: the cockpit feed marks
        // every After-Sales and Parts decision demo, so the lineage screen can never disagree on them.
        var demoMetrics = (await LineageAsync()).GetProperty("metrics").EnumerateArray()
            .Where(m => m.GetProperty("state").GetString() == "demo")
            .Select(m => m.GetProperty("metric").GetString())
            .ToList();

        Assert.Contains("Procurement range", demoMetrics);
        Assert.Contains("Service-intensity index", demoMetrics);
        Assert.Contains("Spare-parts forecast", demoMetrics);
        Assert.Equal(3, demoMetrics.Count);

        var feed = JsonDocument.Parse(
            await factory.CreateClient().GetStringAsync("/api/v1/executive-insights/decision-feed")).RootElement;

        foreach (var d in feed.GetProperty("decisions").EnumerateArray())
        {
            var area = d.GetProperty("area").GetString();
            if (area is "After-Sales" or "Parts")
            {
                Assert.True(
                    d.GetProperty("isDemo").GetBoolean(),
                    $"{area} is synthetic-demo per the platform, so its lineage metric stays demo");
            }
        }
    }

    [Fact]
    public async Task There_is_no_delete_route_under_models()
    {
        var response = await factory.CreateClient().DeleteAsync(LineageUrl);
        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"DELETE {LineageUrl} returned {(int)response.StatusCode}; Lineage is read-only.");
    }

    [Fact]
    public async Task The_served_document_declares_no_delete_under_models()
    {
        var document = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        foreach (var path in document.GetProperty("paths").EnumerateObject())
        {
            if (!path.Name.StartsWith("/api/v1/models", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.False(path.Value.TryGetProperty("delete", out _), $"{path.Name} declares a DELETE.");
        }
    }
}
