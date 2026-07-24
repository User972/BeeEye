using System.Net;
using System.Text.Json;
using Xunit;

namespace BeeEye.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class InventoryApiTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task Summary_reproduces_the_documented_portfolio_profile()
    {
        var client = factory.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/v1/inventory/summary"));
        var summary = doc.RootElement.GetProperty("summary");

        Assert.Equal(291, summary.GetProperty("count").GetInt32());
        Assert.Equal(46_747_500m, summary.GetProperty("value").GetDecimal());
        // Risk bands cover all 291 units.
        var byRisk = summary.GetProperty("byRisk").EnumerateArray().Sum(b => b.GetProperty("units").GetInt32());
        Assert.Equal(291, byRisk);
    }

    [Fact]
    public async Task Items_are_paged_and_sorted_by_risk_descending()
    {
        var client = factory.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/v1/inventory/items?page=1&pageSize=10&sort=risk"));
        var root = doc.RootElement;

        Assert.Equal(291, root.GetProperty("totalCount").GetInt32());
        var items = root.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(10, items.Count);

        var scores = items.Select(i => i.GetProperty("riskScore").GetInt32()).ToList();
        Assert.True(scores.SequenceEqual(scores.OrderByDescending(x => x)), "risk scores must be descending");
    }

    [Fact]
    public async Task Filtering_by_risk_band_restricts_results()
    {
        var client = factory.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/v1/inventory/items?riskBand=Critical&pageSize=100"));
        foreach (var item in doc.RootElement.GetProperty("items").EnumerateArray())
        {
            Assert.Equal("Critical", item.GetProperty("riskBand").GetString());
        }
    }

    [Fact]
    public async Task Item_detail_returns_the_explainable_breakdown()
    {
        var client = factory.CreateClient();
        using var list = JsonDocument.Parse(await client.GetStringAsync("/api/v1/inventory/items?pageSize=1&sort=risk"));
        var stockId = list.RootElement.GetProperty("items")[0].GetProperty("stockId").GetString();

        using var doc = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/inventory/items/{stockId}"));
        var root = doc.RootElement;

        Assert.Equal(stockId, root.GetProperty("stockId").GetString());
        Assert.Equal(5, root.GetProperty("factors").GetArrayLength()); // 5 weighted factors
        Assert.False(string.IsNullOrEmpty(root.GetProperty("recommendation").GetProperty("action").GetString()));
    }

    [Fact]
    public async Task Unknown_item_returns_problem_details_404()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/inventory/items/DOES-NOT-EXIST");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(404, doc.RootElement.GetProperty("status").GetInt32());
    }
}
