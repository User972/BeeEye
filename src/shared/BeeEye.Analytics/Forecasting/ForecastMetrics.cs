namespace BeeEye.Analytics.Forecasting;

/// <summary>
/// Forecast-accuracy metrics. Percentage metrics are nullable and null when their
/// denominator is zero (e.g. WMAPE/bias over all-zero actuals) — never infinite,
/// per the zero-demand forecasting edge case.
/// </summary>
public sealed record AccuracyMetrics(
    double? Wmape,
    double Mae,
    double Rmse,
    double? Bias,
    double BiasAbs,
    double? Mape,
    double OverPct,
    double UnderPct,
    int N);

public static class ForecastMetrics
{
    /// <summary>Computes accuracy of <paramref name="pred"/> against <paramref name="actual"/>.</summary>
    public static AccuracyMetrics Compute(IReadOnlyList<double> actual, IReadOnlyList<double> pred)
    {
        if (pred.Count < actual.Count)
        {
            throw new ArgumentException("pred must be at least as long as actual.", nameof(pred));
        }

        var n = actual.Count;
        var ae = new double[n];
        var se = new double[n];
        var diff = new double[n];
        var mape = new List<double>(n);
        var over = 0;
        var under = 0;

        for (var i = 0; i < n; i++)
        {
            var a = actual[i];
            var f = pred[i];
            var e = f - a;
            ae[i] = Math.Abs(e);
            se[i] = e * e;
            diff[i] = e;
            if (f > a)
            {
                over++;
            }
            else if (f < a)
            {
                under++;
            }

            if (a != 0)
            {
                mape.Add(Math.Abs(e) / Math.Abs(a));
            }
        }

        var actualList = actual as IReadOnlyList<double> ?? actual.ToArray();
        var sa = Statistics.Sum(actualList);

        return new AccuracyMetrics(
            Wmape: sa != 0 ? Statistics.Sum(ae) / sa * 100 : null,
            Mae: Statistics.Mean(ae),
            Rmse: Math.Sqrt(Statistics.Mean(se)),
            Bias: sa != 0 ? Statistics.Sum(diff) / sa * 100 : null,
            BiasAbs: Statistics.Mean(diff),
            Mape: mape.Count > 0 ? Statistics.Mean(mape) * 100 : null,
            OverPct: n > 0 ? (double)over / n * 100 : 0,
            UnderPct: n > 0 ? (double)under / n * 100 : 0,
            N: n);
    }
}
