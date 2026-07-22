namespace BeeEye.Analytics.Forecasting;

public sealed record ForecastOptions(int Horizon = 6, int Holdout = 6, string? Algo = null, int Ci = 80);

public sealed record HistoryPoint(string Month, string Label, double Value, bool IsHold);

public sealed record BacktestPoint(string Month, string Label, double Actual, double Forecast);

public sealed record FuturePoint(string Month, string Label, double Value, double Lo, double Hi);

public sealed record MethodComparison(
    string Key, string Name, double? Wmape, double? Bias, double Mae, double Rmse, bool IsBest, bool IsChosen);

public sealed record ForecastExplanation(IReadOnlyList<string> Points, double Recent3, double Prior12, double ChangePct);

public sealed record ForecastResult(
    IReadOnlyList<HistoryPoint> History,
    IReadOnlyList<BacktestPoint> Backtest,
    IReadOnlyList<FuturePoint> Future,
    double FutureSum,
    string Chosen,
    string ChosenName,
    string Best,
    IReadOnlyList<MethodComparison> Methods,
    AccuracyMetrics Accuracy,
    double Sigma,
    int Holdout,
    int Horizon,
    int TrainN,
    int TotalN,
    double HistUnits,
    string LastMonth,
    ForecastExplanation Explanation);

/// <summary>
/// Back-tests the baseline methods on a holdout, selects the lowest-WMAPE model,
/// refits on the full series and projects the future with residual-based confidence
/// intervals. Direct port of engine.js <c>forecast()</c>.
/// </summary>
public static class Forecaster
{
    private static readonly string[] Keys = ["naive", "ma3", "snaive", "hw"];

    public static ForecastResult Run(
        IReadOnlyList<double> series,
        IReadOnlyList<string> months,
        ForecastOptions options,
        double? ramadanLiftPct = null)
    {
        if (series.Count != months.Count)
        {
            throw new ArgumentException("series and months must be the same length.");
        }

        var y = series;
        var n = y.Count;
        var h = Math.Clamp(options.Horizon, 1, 36);
        var hold = Math.Min(options.Holdout, n - 12 > 0 ? n - 12 : 6);
        hold = Math.Max(1, Math.Min(hold, n - 1));

        var trainY = ForecastMethods.Slice(y, 0, n - hold);
        var holdY = ForecastMethods.Slice(y, n - hold, n);

        var results = new Dictionary<string, (MethodResult Method, AccuracyMetrics Metrics)>();
        foreach (var key in Keys)
        {
            var mr = Method(key, trainY, hold);
            results[key] = (mr, ForecastMetrics.Compute(holdY, mr.Fc));
        }

        var best = Keys
            .Where(k => results[k].Metrics.Wmape is not null)
            .OrderBy(k => results[k].Metrics.Wmape)
            .FirstOrDefault() ?? "hw";

        var chosen = options.Algo is not null && Array.IndexOf(Keys, options.Algo) >= 0 ? options.Algo : best;

        var full = Method(chosen, y, h);
        var resid = new List<double>(n);
        for (var i = 0; i < n; i++)
        {
            resid.Add(y[i] - full.Fitted[i]);
        }

        var sigma = Statistics.Std(resid);
        var z = options.Ci == 95 ? 1.96 : options.Ci == 90 ? 1.645 : 1.28;
        var lastMonth = months[^1];

        var future = new List<FuturePoint>(h);
        for (var k = 0; k < h; k++)
        {
            var month = MonthKey.Add(lastMonth, k + 1);
            var v = Math.Max(0, full.Fc[k]);
            var band = z * sigma * Math.Sqrt(1 + (0.15 * k));
            future.Add(new FuturePoint(month, MonthKey.Label(month), v, Math.Max(0, v - band), v + band));
        }

        var btMethod = Method(chosen, trainY, hold);
        var backtest = new List<BacktestPoint>(hold);
        for (var i = 0; i < hold; i++)
        {
            var mk = months[n - hold + i];
            backtest.Add(new BacktestPoint(mk, MonthKey.Label(mk), holdY[i], Math.Max(0, btMethod.Fc[i])));
        }

        var history = new List<HistoryPoint>(n);
        for (var i = 0; i < n; i++)
        {
            history.Add(new HistoryPoint(months[i], MonthKey.Label(months[i]), y[i], i >= n - hold));
        }

        var methods = Keys.Select(key =>
        {
            var m = results[key];
            return new MethodComparison(
                key, m.Method.Name, m.Metrics.Wmape, m.Metrics.Bias, m.Metrics.Mae, m.Metrics.Rmse,
                key == best, key == chosen);
        }).ToList();

        var explanation = Explain(y, future, results[chosen].Method.Name, results[chosen].Metrics, ramadanLiftPct);

        return new ForecastResult(
            history, backtest, future, future.Sum(f => f.Value),
            chosen, results[chosen].Method.Name, best, methods, results[chosen].Metrics,
            sigma, hold, h, n - hold, n, Statistics.Sum(y), lastMonth, explanation);
    }

    private static MethodResult Method(string key, IReadOnlyList<double> y, int h) => key switch
    {
        "naive" => ForecastMethods.Naive(y, h),
        "ma3" => ForecastMethods.MovingAvg(y, h, 3),
        "snaive" => ForecastMethods.SeasonalNaive(y, h, 12),
        "hw" => ForecastMethods.HoltWinters(y, 12, h, 0.35, 0.08, 0.3),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown forecast method."),
    };

    private static ForecastExplanation Explain(
        IReadOnlyList<double> y,
        IReadOnlyList<FuturePoint> future,
        string chosenName,
        AccuracyMetrics accuracy,
        double? ramadanLiftPct)
    {
        var n = y.Count;
        var recent3 = Statistics.Mean(ForecastMethods.Slice(y, n - 3, n));
        var prior12 = Statistics.Mean(ForecastMethods.Slice(y, n - 15, n - 3));
        var chg = prior12 != 0 ? (recent3 - prior12) / prior12 * 100 : 0;

        var points = new List<string>();
        if (chg > 8)
        {
            points.Add($"Recent 3-month demand ({recent3:F0}/mo) is above the prior 12-month average ({prior12:F0}/mo).");
        }
        else if (chg < -8)
        {
            points.Add($"Recent 3-month demand ({recent3:F0}/mo) has slowed versus the prior 12-month average ({prior12:F0}/mo).");
        }
        else
        {
            points.Add("Demand has been broadly stable versus the prior 12-month average.");
        }

        if (ramadanLiftPct is { } lift && Math.Abs(lift) > 5)
        {
            var sign = lift >= 0 ? "+" : "";
            points.Add($"Periods flagged as Ramadan show {sign}{lift:F0}% monthly volume versus non-Ramadan periods (association, not proven cause).");
        }

        var direction = (future.Count > 0 ? future.Average(f => f.Value) : 0) - recent3;
        points.Add($"The {chosenName} model projects a {(direction >= 0 ? "continuation/uplift" : "moderation")} over the next {future.Count} months.");

        var wm = accuracy.Wmape;
        var confidence = wm is null ? "low" : wm < 15 ? "high" : wm < 30 ? "medium" : "low";
        points.Add($"Back-test WMAPE is {(wm is null ? "n/a" : $"{wm:F1}%")}; confidence is {confidence}.");

        return new ForecastExplanation(points, recent3, prior12, chg);
    }
}
