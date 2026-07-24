using System.Globalization;
using BeeEye.Analytics.Inventory;
using BeeEye.Modules.PlatformAdministration.Contracts;

namespace BeeEye.Modules.PlatformAdministration.Application;

/// <summary>
/// Projects the platform's real risk configuration (V3-GOV-010) for the read-only Settings screen. It
/// reads <see cref="RiskSettings.Default"/> and <see cref="RiskWeights"/> directly and re-types nothing:
/// the thresholds come from the settings, and every band label comes from <see cref="Bands"/>, so the
/// screen tracks the engine automatically.
/// </summary>
public sealed class SettingsReadService
{
    private const string ReadOnlyNote =
        "These are the platform's current configuration values, shown read-only — the decision models "
        + "read them exactly as displayed. Editing configuration is governed separately (a versioned, "
        + "audited change that recomputes every risk score) and is not available on this screen.";

    public SettingsResponse Build()
    {
        var settings = RiskSettings.Default;
        var w = settings.Weights;

        var sum = w.Cover + w.Aging + w.Demand + w.Holding + w.Lead;
        var weights = new RiskWeightsDto(
            w.Cover, w.Aging, w.Demand, w.Holding, w.Lead, sum,
            $"The engine renormalises the weights by their sum ({sum.ToString("0.#", CultureInfo.InvariantCulture)}), "
            + "so they need not total 100.");

        return new SettingsResponse(
            weights,
            BuildBands(settings.RiskBands, threshold => Bands.Risk(threshold, settings.RiskBands)),
            BuildBands(settings.AgingBands, threshold => Bands.Aging(threshold, settings.AgingBands)),
            settings.AnalysisDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
            settings.TrailingMonths,
            settings.CoverMax,
            ReadOnlyNote);
    }

    /// <summary>
    /// Turns a threshold array into labelled bands. The label for each band is taken from
    /// <paramref name="labelAt"/> — i.e. from <see cref="Bands"/> itself — so labels are never duplicated
    /// here. Threshold i is the inclusive upper bound of band i; the final band is open-ended.
    /// </summary>
    private static IReadOnlyList<BandDto> BuildBands(int[] thresholds, Func<int, string> labelAt)
    {
        var bands = new List<BandDto>(thresholds.Length + 1);
        var lower = 0;

        foreach (var upper in thresholds)
        {
            bands.Add(new BandDto(labelAt(upper), upper, $"{lower}–{upper}"));
            lower = upper + 1;
        }

        // The open-ended top band's label comes from a score one past the last threshold.
        bands.Add(new BandDto(labelAt(thresholds[^1] + 1), null, $"{lower}+"));
        return bands;
    }
}
