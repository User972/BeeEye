using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace BeeEye.IntegrationTests;

/// <summary>
/// End-to-end tests for the read-only Settings screen (V3-GOV-010). They pin the served configuration to
/// the documented engine values, and pin the deliberate absence of a cover-target field.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class SettingsApiTests(IntegrationTestFactory factory)
{
    private const string SettingsUrl = "/api/v1/platform-admin/settings";

    private async Task<(JsonElement Root, string Raw)> SettingsAsync()
    {
        var raw = await factory.CreateClient().GetStringAsync(SettingsUrl);
        return (JsonDocument.Parse(raw).RootElement.Clone(), raw);
    }

    [Fact]
    public async Task Module_reports_itself_as_operational()
    {
        using var doc = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/api/v1/platform-admin"));
        Assert.Equal("operational", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Settings_is_reachable_and_returns_ok()
    {
        var response = await factory.CreateClient().GetAsync(SettingsUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Risk_weights_equal_the_engine_defaults()
    {
        var (root, _) = await SettingsAsync();
        var weights = root.GetProperty("weights");

        Assert.Equal(30d, weights.GetProperty("cover").GetDouble());
        Assert.Equal(25d, weights.GetProperty("aging").GetDouble());
        Assert.Equal(20d, weights.GetProperty("demand").GetDouble());
        Assert.Equal(15d, weights.GetProperty("holding").GetDouble());
        Assert.Equal(10d, weights.GetProperty("lead").GetDouble());
        Assert.Equal(100d, weights.GetProperty("sum").GetDouble());
    }

    [Fact]
    public async Task Risk_bands_carry_the_engine_thresholds_and_labels()
    {
        var (root, _) = await SettingsAsync();
        var bands = root.GetProperty("riskBands").EnumerateArray().ToList();

        Assert.Equal(["Low", "Medium", "High", "Critical"], bands.Select(b => b.GetProperty("label").GetString()!).ToArray());

        var thresholds = bands
            .Where(b => b.GetProperty("threshold").ValueKind != JsonValueKind.Null)
            .Select(b => b.GetProperty("threshold").GetInt32())
            .ToArray();
        Assert.Equal([34, 59, 79], thresholds);
    }

    [Fact]
    public async Task Aging_bands_carry_the_engine_thresholds_and_labels()
    {
        var (root, _) = await SettingsAsync();
        var bands = root.GetProperty("agingBands").EnumerateArray().ToList();

        Assert.Equal(
            ["New", "Healthy", "Watch", "High attention", "Critical aging"],
            bands.Select(b => b.GetProperty("label").GetString()!).ToArray());

        var thresholds = bands
            .Where(b => b.GetProperty("threshold").ValueKind != JsonValueKind.Null)
            .Select(b => b.GetProperty("threshold").GetInt32())
            .ToArray();
        Assert.Equal([30, 60, 90, 120], thresholds);
    }

    [Fact]
    public async Task Analysis_date_horizon_and_cover_max_equal_the_engine()
    {
        var (root, _) = await SettingsAsync();

        Assert.Equal("30 Jun 2026", root.GetProperty("analysisDate").GetString());
        Assert.Equal(3, root.GetProperty("trailingMonths").GetInt32());
        Assert.Equal(6d, root.GetProperty("coverMax").GetDouble());
    }

    [Fact]
    public async Task The_response_carries_no_cover_target_anywhere()
    {
        var (_, raw) = await SettingsAsync();

        // The wireframe's coverTarget was never ported to RiskSettings; the transparency screen must not
        // invent it. (coverMax legitimately contains "cover" but never "target".)
        Assert.DoesNotContain("target", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_screen_declares_itself_read_only()
    {
        var (root, _) = await SettingsAsync();
        Assert.Contains("read-only", root.GetProperty("note").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task There_is_no_delete_route_under_platform_admin()
    {
        var response = await factory.CreateClient().DeleteAsync(SettingsUrl);
        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"DELETE {SettingsUrl} returned {(int)response.StatusCode}; Settings is read-only.");
    }

    [Fact]
    public async Task The_served_document_declares_no_delete_under_platform_admin()
    {
        var document = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        foreach (var path in document.GetProperty("paths").EnumerateObject())
        {
            if (!path.Name.StartsWith("/api/v1/platform-admin", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.False(path.Value.TryGetProperty("delete", out _), $"{path.Name} declares a DELETE.");
        }
    }
}
