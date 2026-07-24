using System.Net;
using System.Text.Json;
using Xunit;

namespace BeeEye.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class AfterSalesApiTests(IntegrationTestFactory factory)
{
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
}
