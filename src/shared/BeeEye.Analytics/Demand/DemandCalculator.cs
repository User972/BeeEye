using System.Globalization;

namespace BeeEye.Analytics.Demand;

/// <summary>
/// Demand velocity and trend, ported from engine.js. Velocity uses the documented
/// four-tier fallback hierarchy so a sparse location-model-variant cell is never
/// silently treated as zero demand.
/// </summary>
public static class DemandCalculator
{
    public static DemandVelocityResult Velocity(
        DemandAggregates agg, string loc, string model, string variant, int nMonths = 3, string? endMonth = null)
    {
        // Mirror engine.js `nMonths || 3`: a zero/negative window falls back to three months
        // rather than collapsing to an empty trailing window (which would force every cell to Tier 4).
        if (nMonths <= 0)
        {
            nMonths = 3;
        }

        var window = MonthKey.Trailing(nMonths, endMonth ?? agg.LastMonth);

        // Tier 1 — location · model · variant.
        var lmvVals = window.Select(m => agg.Lmv(loc, model, variant, m)).ToArray();
        if (Statistics.Sum(lmvVals) > 0)
        {
            var nz = lmvVals.Count(v => v > 0);
            return new DemandVelocityResult(
                Statistics.Mean(lmvVals),
                "Location-model-variant demand",
                nz >= 2 ? "High" : "Medium",
                $"Trailing {nMonths}-month average at this location.");
        }

        // Tier 2 — national model · variant scaled by this location's historical share.
        var mvVals = window.Select(m => agg.Mv(model, variant, m)).ToArray();
        var mvTot = agg.MvTot(model, variant);
        var share = mvTot != 0 ? agg.LmvTot(loc, model, variant) / mvTot : 0;
        if (Statistics.Sum(mvVals) > 0 && share > 0)
        {
            return new DemandVelocityResult(
                Statistics.Mean(mvVals) * share,
                "National model-variant fallback",
                "Medium",
                $"National {model} {variant} demand scaled by this location's {(share * 100).ToString("F1", CultureInfo.InvariantCulture)}% historical share.");
        }

        // Tier 3 — model-level national demand divided across selling locations.
        var mdlVals = window.Select(m => agg.Mdl(model, m)).ToArray();
        var nLoc = Math.Max(1, agg.ModelLocationCount(model));
        if (Statistics.Sum(mdlVals) > 0)
        {
            return new DemandVelocityResult(
                Statistics.Mean(mdlVals) / nLoc,
                "Model-level fallback",
                "Low",
                $"National {model} demand divided across {nLoc} selling locations.");
        }

        // Tier 4 — nothing reliable.
        return new DemandVelocityResult(0, "Insufficient demand history", "Low", "No reliable recent demand signal for this combination.");
    }

    public static DemandTrendResult Trend(
        DemandAggregates agg, string loc, string model, string variant, bool useNational = false)
    {
        var recent = MonthKey.Trailing(3, agg.LastMonth);
        var prior = MonthKey.Trailing(3, MonthKey.Add(agg.LastMonth, -3));

        double Pull(string mk) => useNational ? agg.Mv(model, variant, mk) : agg.Lmv(loc, model, variant, mk);

        var r = Statistics.Mean(recent.Select(Pull).ToArray());
        var p = Statistics.Mean(prior.Select(Pull).ToArray());

        if (p == 0 && r == 0)
        {
            r = Statistics.Mean(recent.Select(m => agg.Mv(model, variant, m)).ToArray());
            p = Statistics.Mean(prior.Select(m => agg.Mv(model, variant, m)).ToArray());
        }

        var chg = p != 0 ? (r - p) / p * 100 : r > 0 ? 100 : 0;
        var dir = chg > 8 ? "increasing" : chg < -8 ? "declining" : "stable";
        return new DemandTrendResult(r, p, chg, dir);
    }
}
