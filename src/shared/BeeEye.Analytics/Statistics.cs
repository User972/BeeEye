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
