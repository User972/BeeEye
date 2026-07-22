using System.Net;
using System.Text.Json;
using Xunit;

namespace BeeEye.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class ConfigurationApiTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task Config_summary_classifies_the_configuration_mix()
    {
        var client = factory.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/v1/sales-actuals/config-demand/summary"));
        var summary = doc.RootElement.GetProperty("summary");

        Assert.True(summary.GetProperty("configurations").GetInt32() > 0);
        var byRotation = summary.GetProperty("byRotation").EnumerateArray().ToList();
        Assert.Equal(4, byRotation.Count); // Fast / Medium / Slow / Dead
        var configCount = byRotation.Sum(b => b.GetProperty("configurations").GetInt32());
        Assert.Equal(summary.GetProperty("configurations").GetInt32(), configCount);
    }

    [Fact]
    public async Task Configs_are_paged_and_default_sorted_by_total_units()
    {
        var client = factory.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/v1/sales-actuals/config-demand/configs?pageSize=5"));
        var items = doc.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("totalUnits").GetDouble()).ToList();

        Assert.True(items.Count is > 0 and <= 5);
        Assert.True(items.SequenceEqual(items.OrderByDescending(x => x)), "default sort is total units descending");
    }

    [Fact]
    public async Task Decay_alerts_only_return_configs_flagged_for_decay()
    {
        var client = factory.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/v1/sales-actuals/config-demand/decay-alerts"));
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            Assert.True(row.GetProperty("decayAlert").GetBoolean());
        }
    }

    [Fact]
    public async Task Unknown_configuration_returns_404()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/sales-actuals/config-demand/config?model=Nope&variant=X&colour=Y&interior=Z");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
