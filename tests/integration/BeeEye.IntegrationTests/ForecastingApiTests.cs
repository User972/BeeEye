using System.Net;
using System.Text.Json;
using Xunit;

namespace BeeEye.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class ForecastingApiTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task Forecast_returns_backtest_chosen_model_and_future_with_ci()
    {
        var client = factory.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/v1/forecasting/forecast?horizon=6&holdout=6"));
        var f = doc.RootElement.GetProperty("forecast");

        Assert.False(string.IsNullOrEmpty(f.GetProperty("chosenName").GetString()));
        Assert.Equal(6, f.GetProperty("future").GetArrayLength());
        Assert.Equal(6, f.GetProperty("backtest").GetArrayLength());
        Assert.Equal(4, f.GetProperty("methods").GetArrayLength());

        // Confidence band widens across the horizon.
        var future = f.GetProperty("future").EnumerateArray().ToList();
        double Width(JsonElement p) => p.GetProperty("hi").GetDouble() - p.GetProperty("lo").GetDouble();
        Assert.True(Width(future[^1]) >= Width(future[0]));
    }

    [Fact]
    public async Task Accuracy_by_model_flags_over_and_under_forecasting()
    {
        var client = factory.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/v1/forecasting/accuracy-by/model?holdout=6"));
        var rows = doc.RootElement.GetProperty("rows").EnumerateArray().ToList();

        Assert.NotEmpty(rows);
        // Every model row carries a tendency classification.
        foreach (var row in rows)
        {
            var tendency = row.GetProperty("tendency").GetString();
            Assert.Contains(tendency, new[] { "over-forecasting", "under-forecasting", "balanced", "insufficient" });
        }
    }

    [Fact]
    public async Task Accuracy_by_unknown_dimension_returns_400()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/forecasting/accuracy-by/not-a-dimension");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Health_ready_is_healthy_with_the_database_up()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
