using System.Globalization;
using System.Reflection;
using BeeEye.Analytics.Inventory;
using BeeEye.Modules.PlatformAdministration.Application;
using BeeEye.Modules.PlatformAdministration.Contracts;
using Xunit;

namespace BeeEye.UnitTests.PlatformAdministration;

/// <summary>
/// Tests for <see cref="SettingsReadService"/> (V3-GOV-010). The screen exists to be trustworthy, so the
/// assertions tie every surfaced value back to the C# constant it claims to describe — a drift in either
/// direction fails here. And they pin the deliberate absence of a cover-target: the wireframe's
/// <c>coverTarget</c> was never ported, and this screen must not invent it.
/// </summary>
public sealed class SettingsServiceTests
{
    private static SettingsResponse Build() => new SettingsReadService().Build();

    [Fact]
    public void Weights_equal_the_engine_defaults()
    {
        var w = Build().Weights;
        var d = RiskSettings.Default.Weights;

        Assert.Equal(d.Cover, w.Cover);
        Assert.Equal(d.Aging, w.Aging);
        Assert.Equal(d.Demand, w.Demand);
        Assert.Equal(d.Holding, w.Holding);
        Assert.Equal(d.Lead, w.Lead);
        Assert.Equal(d.Cover + d.Aging + d.Demand + d.Holding + d.Lead, w.Sum);
        Assert.Equal(30d, w.Cover); // pinned to the documented value
        Assert.Contains("renormalise", w.Note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Risk_band_thresholds_equal_the_engine_and_labels_come_from_bands()
    {
        var bands = Build().RiskBands;

        Assert.Equal(["Low", "Medium", "High", "Critical"], bands.Select(b => b.Label).ToArray());
        // Non-null thresholds are exactly RiskSettings.RiskBands; the top band is open-ended.
        Assert.Equal(RiskSettings.Default.RiskBands, bands.Where(b => b.Threshold.HasValue).Select(b => b.Threshold!.Value).ToArray());
        Assert.Null(bands[^1].Threshold);
        Assert.Equal("0–34", bands[0].Range);
        Assert.Equal("80+", bands[^1].Range);
    }

    [Fact]
    public void Aging_band_thresholds_equal_the_engine_and_labels_come_from_bands()
    {
        var bands = Build().AgingBands;

        Assert.Equal(
            ["New", "Healthy", "Watch", "High attention", "Critical aging"],
            bands.Select(b => b.Label).ToArray());
        Assert.Equal(RiskSettings.Default.AgingBands, bands.Where(b => b.Threshold.HasValue).Select(b => b.Threshold!.Value).ToArray());
        Assert.Equal("0–30", bands[0].Range);
        Assert.Equal("121+", bands[^1].Range);
    }

    [Fact]
    public void Analysis_date_horizon_and_cover_max_equal_the_engine()
    {
        var response = Build();
        var d = RiskSettings.Default;

        Assert.Equal(d.AnalysisDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), response.AnalysisDate);
        Assert.Equal("30 Jun 2026", response.AnalysisDate);
        Assert.Equal(d.TrailingMonths, response.TrailingMonths);
        Assert.Equal(3, response.TrailingMonths);
        Assert.Equal(d.CoverMax, response.CoverMax);
        Assert.Equal(6d, response.CoverMax);
    }

    [Fact]
    public void The_note_states_the_configuration_is_read_only()
    {
        Assert.Contains("read-only", Build().Note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_engine_settings_still_have_no_cover_target_member()
    {
        // Guards the gotcha: if anyone ports coverTarget onto RiskSettings, this fires so the screen is
        // revisited deliberately rather than silently surfacing a half-wired value.
        var members = typeof(RiskSettings)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Select(m => m.Name);

        Assert.DoesNotContain(members, name => name.Contains("CoverTarget", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_response_exposes_no_cover_target_field()
    {
        foreach (var type in new[] { typeof(SettingsResponse), typeof(RiskWeightsDto), typeof(BandDto) })
        {
            var props = type.GetProperties().Select(p => p.Name);
            Assert.DoesNotContain(props, name => name.Contains("CoverTarget", StringComparison.OrdinalIgnoreCase));
        }
    }
}
