namespace BeeEye.Analytics;

/// <summary>Small numeric helpers ported verbatim from the wireframe engine.</summary>
public static class Statistics
{
    public static double Sum(IReadOnlyList<double> values)
    {
        var s = 0.0;
        for (var i = 0; i < values.Count; i++)
        {
            s += values[i];
        }

        return s;
    }

    public static double Mean(IReadOnlyList<double> values) => values.Count == 0 ? 0 : Sum(values) / values.Count;

    /// <summary>Population standard deviation (divides by N), matching engine.js.</summary>
    public static double Std(IReadOnlyList<double> values)
    {
        if (values.Count < 2)
        {
            return 0;
        }

        var m = Mean(values);
        var squared = new double[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var d = values[i] - m;
            squared[i] = d * d;
        }

        return Math.Sqrt(Mean(squared));
    }

    public static double Clamp(double x, double lo, double hi) => Math.Max(lo, Math.Min(hi, x));

    /// <summary>Sample coefficient of variation (Std / Mean). Returns 0 when the mean is 0.</summary>
    public static double CoefficientOfVariation(IReadOnlyList<double> values)
    {
        var m = Mean(values);
        return m == 0 ? 0 : Std(values) / m;
    }

    /// <summary>
    /// Pearson product-moment correlation of two equal-length series. Returns
    /// <c>null</c> when the series are too short (&lt; 2 points) or either has zero
    /// variance (correlation is undefined), so callers surface "no association" rather
    /// than a fabricated coefficient. This is an <b>association</b> measure, not causation.
    /// </summary>
    public static double? Correlation(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        var n = Math.Min(x.Count, y.Count);
        if (n < 2)
        {
            return null;
        }

        double mx = 0, my = 0;
        for (var i = 0; i < n; i++)
        {
            mx += x[i];
            my += y[i];
        }

        mx /= n;
        my /= n;

        double sxy = 0, sxx = 0, syy = 0;
        for (var i = 0; i < n; i++)
        {
            var dx = x[i] - mx;
            var dy = y[i] - my;
            sxy += dx * dy;
            sxx += dx * dx;
            syy += dy * dy;
        }

        if (sxx <= 0 || syy <= 0)
        {
            return null;
        }

        return Clamp(sxy / Math.Sqrt(sxx * syy), -1, 1);
    }

    /// <summary>
    /// Returns a function that maps a value to its percentile rank (0..100) within
    /// the supplied population. Ties rank at the lower bound, matching engine.js.
    /// </summary>
    public static Func<double, double> PercentileRanker(IReadOnlyList<double> values)
    {
        var sorted = values.ToArray();
        Array.Sort(sorted);
        var n = sorted.Length;
        return x =>
        {
            if (n < 2)
            {
                return 50;
            }

            var i = 0;
            while (i < n && sorted[i] < x)
            {
                i++;
            }

            return (double)i / (n - 1) * 100;
        };
    }
}
