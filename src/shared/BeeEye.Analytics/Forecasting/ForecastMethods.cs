namespace BeeEye.Analytics.Forecasting;

/// <summary>Output of a forecast method: in-sample fit and out-of-sample forecast.</summary>
public sealed record MethodResult(IReadOnlyList<double> Fitted, IReadOnlyList<double> Fc, string Name);

/// <summary>
/// The transparent forecast methods from engine.js. A candidate ML model must beat
/// the best of these on time-based back-testing before it earns its place.
/// </summary>
public static class ForecastMethods
{
    public static MethodResult Naive(IReadOnlyList<double> y, int h)
    {
        var v = y.Count > 0 ? y[^1] : 0;
        var fitted = new double[y.Count];
        for (var i = 0; i < y.Count; i++)
        {
            fitted[i] = i > 0 ? y[i - 1] : y[0];
        }

        return new MethodResult(fitted, Filled(h, v), "Naïve (last month)");
    }

    public static MethodResult MovingAvg(IReadOnlyList<double> y, int h, int k = 3)
    {
        var v = Statistics.Mean(Slice(y, y.Count - k, y.Count));
        var fitted = new double[y.Count];
        for (var i = 0; i < y.Count; i++)
        {
            fitted[i] = i < k ? Statistics.Mean(Slice(y, 0, i + 1)) : Statistics.Mean(Slice(y, i - k, i));
        }

        return new MethodResult(fitted, Filled(h, Math.Max(0, v)), $"{k}-month moving average");
    }

    public static MethodResult SeasonalNaive(IReadOnlyList<double> y, int h, int m = 12)
    {
        if (y.Count < m)
        {
            return Naive(y, h);
        }

        var fc = new double[h];
        for (var k = 1; k <= h; k++)
        {
            fc[k - 1] = Math.Max(0, y[y.Count - m + ((k - 1) % m)]);
        }

        var fitted = new double[y.Count];
        for (var i = 0; i < y.Count; i++)
        {
            fitted[i] = i >= m ? y[i - m] : y[i];
        }

        return new MethodResult(fitted, fc, "Seasonal naïve (last year)");
    }

    public static MethodResult HoltLinear(IReadOnlyList<double> y, int h, double alpha = 0.4, double beta = 0.1)
    {
        var n = y.Count;
        if (n == 0)
        {
            return new MethodResult([], Filled(h, 0), "Holt");
        }

        var level = y[0];
        var trend = n > 1 ? y[1] - y[0] : 0;
        var fitted = new double[n];
        fitted[0] = level;
        for (var t = 1; t < n; t++)
        {
            fitted[t] = level + trend;
            var ln = (alpha * y[t]) + ((1 - alpha) * (level + trend));
            trend = (beta * (ln - level)) + ((1 - beta) * trend);
            level = ln;
        }

        var fc = new double[h];
        for (var k = 1; k <= h; k++)
        {
            fc[k - 1] = Math.Max(0, level + (k * trend));
        }

        return new MethodResult(fitted, fc, "Holt");
    }

    public static MethodResult HoltWinters(
        IReadOnlyList<double> y, int m, int h, double alpha, double beta, double gamma)
    {
        var n = y.Count;
        if (n < m + 2)
        {
            return HoltLinear(y, h, alpha, beta);
        }

        var seasons = n / m;
        var level = Statistics.Mean(Slice(y, 0, m));
        var trend = (Statistics.Mean(Slice(y, m, 2 * m)) - Statistics.Mean(Slice(y, 0, m))) / m;
        if (!double.IsFinite(trend))
        {
            trend = 0;
        }

        var s0 = new double[m];
        for (var i = 0; i < m; i++)
        {
            var devs = new List<double>(seasons);
            for (var s = 0; s < seasons; s++)
            {
                var seg = Slice(y, s * m, (s * m) + m);
                if ((s * m) + i < n)
                {
                    devs.Add(y[(s * m) + i] - Statistics.Mean(seg));
                }
            }

            s0[i] = Statistics.Mean(devs);
        }

        var sh = new double?[n + m];
        for (var i = 0; i < m; i++)
        {
            sh[i] = s0[i];
        }

        var l = level;
        var tr = trend;
        var fitted = new double[n];
        for (var t = 0; t < n; t++)
        {
            var sv = SeasonalAt(sh, t, m, s0);
            fitted[t] = l + tr + sv;
            var ln = (alpha * (y[t] - sv)) + ((1 - alpha) * (l + tr));
            var tn = (beta * (ln - l)) + ((1 - beta) * tr);
            var sn = (gamma * (y[t] - ln)) + ((1 - gamma) * sv);
            sh[t + m] = sn;
            l = ln;
            tr = tn;
        }

        var fc = new double[h];
        for (var k = 1; k <= h; k++)
        {
            fc[k - 1] = Math.Max(0, l + (k * tr) + SeasonalAt(sh, n + k - 1, m, s0));
        }

        return new MethodResult(fitted, fc, "Holt-Winters");
    }

    private static double SeasonalAt(double?[] sh, int idx, int m, double[] s0)
    {
        var i = idx;
        while (i >= sh.Length || sh[i] is null)
        {
            i -= m;
            if (i < 0)
            {
                return s0[((idx % m) + m) % m];
            }
        }

        return sh[i]!.Value;
    }

    private static double[] Filled(int h, double v)
    {
        var a = new double[h];
        Array.Fill(a, v);
        return a;
    }

    /// <summary>JS-style Array.slice(start, end) — end exclusive, clamped to bounds.</summary>
    internal static IReadOnlyList<double> Slice(IReadOnlyList<double> y, int start, int end)
    {
        start = Math.Max(0, start);
        end = Math.Min(y.Count, end);
        if (end <= start)
        {
            return [];
        }

        var outp = new double[end - start];
        for (var i = start; i < end; i++)
        {
            outp[i - start] = y[i];
        }

        return outp;
    }
}
