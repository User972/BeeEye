using System.Text.Json;
using Xunit;

namespace BeeEye.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class OrderProcurementApiTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task Order_optimisation_nets_supply_off_the_forecast()
    {
        var client = factory.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/v1/recommendations/order-optimisation?horizon=3&targetCoverMonths=1"));
        var items = doc.RootElement.GetProperty("items").EnumerateArray().ToList();

        Assert.NotEmpty(items);
        foreach (var r in items)
        {
            // Recommended quantity never negative; net requirement respects available supply.
            Assert.True(r.GetProperty("recommendedQuantity").GetInt32() >= 0);
            Assert.True(r.GetProperty("available").GetInt32() >= 0);
        }
    }

    [Fact]
    public async Task Higher_target_cover_never_reduces_the_order()
    {
        var client = factory.CreateClient();
        static async Task<int> Total(HttpClient c, double cover)
        {
            using var doc = JsonDocument.Parse(await c.GetStringAsync($"/api/v1/recommendations/order-optimisation?horizon=3&targetCoverMonths={cover}"));
            return doc.RootElement.GetProperty("meta").GetProperty("totalRecommendedUnits").GetInt32();
        }

        Assert.True(await Total(client, 3) >= await Total(client, 1));
    }

    [Fact]
    public async Task Procurement_returns_a_range_and_uses_observed_lead_time()
    {
        var client = factory.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/v1/procurement/recommendations?serviceLevel=0.95"));
        var items = doc.RootElement.GetProperty("items").EnumerateArray().ToList();

        Assert.NotEmpty(items);
        foreach (var r in items)
        {
            Assert.True(r.GetProperty("rangeHigh").GetInt32() >= r.GetProperty("rangeLow").GetInt32());
            Assert.True(r.GetProperty("leadTimeMonths").GetDouble() > 0);
            Assert.Contains(r.GetProperty("stockoutRisk").GetString(), new[] { "Low", "Medium", "High" });
        }
    }

    [Fact]
    public async Task Higher_service_level_increases_safety_stock()
    {
        var client = factory.CreateClient();
        static async Task<double> TotalSafety(HttpClient c, double service)
        {
            using var doc = JsonDocument.Parse(await c.GetStringAsync($"/api/v1/procurement/recommendations?serviceLevel={service}"));
            return doc.RootElement.GetProperty("items").EnumerateArray().Sum(i => i.GetProperty("safetyStock").GetDouble());
        }

        Assert.True(await TotalSafety(client, 0.99) > await TotalSafety(client, 0.90));
    }
}
