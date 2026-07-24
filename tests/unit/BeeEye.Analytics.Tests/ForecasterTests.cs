using BeeEye.Analytics;
using BeeEye.Analytics.Forecasting;
using Xunit;

namespace BeeEye.Analytics.Tests;

/// <summary>
/// Tests for <see cref="Forecaster.Run"/> — the back-test / select / refit / project
/// pipeline. Deterministic synthetic series only; complex iterative methods
/// (Holt-Winters, the full Run) are asserted via invariants and structural properties,
/// while the simple overrides (naive / ma3 / snaive) are checked against hand-derived
/// values pulled straight from the input series.
/// </summary>
public sealed class ForecasterTests
{
    // ---- Synthetic series builders --------------------------------------------------

    // A clear seasonal (period-12) + linear-trend pattern with a small deterministic
    // wiggle that is NOT aligned to the 12-month period, so no method can fit it
    // perfectly => residual sigma > 0 for every candidate model.
    private static readonly int[] Seasonal =
        { -30, -10, 25, 55, 70, 50, 20, -15, -45, -60, -40, -20 };

    private static double Val(int i)
        => 400 + (5.0 * i) + Seasonal[i % 12] + (((i * 7) % 13) - 6);

    private static double[] SeasonalTrend(int n)
    {
        var a = new double[n];
        for (var i = 0; i < n; i++)
        {
            a[i] = Val(i);
        }

        return a;
    }

    // Exactly n consecutive month keys starting at 2023-01.
    private static string[] Months(int n, string start = "2023-01")
        => MonthKey.Range(start, MonthKey.Add(start, n - 1)).ToArray();

    private static (double[] Series, string[] Months) Build(int n, string start = "2023-01")
        => (SeasonalTrend(n), Months(n, start));

    private static double MeanRange(double[] a, int start, int count)
    {
        var s = 0.0;
        for (var i = 0; i < count; i++)
        {
            s += a[start + i];
        }

        return s / count;
    }

    // ================================================================================
    //  Length-mismatch guard
    // ================================================================================

    [Fact]
    public void Run_SeriesAndMonthsLengthMismatch_Throws()
    {
        var series = new double[] { 1, 2, 3, 4, 5 };
        var months = Months(4); // one short on purpose

        Assert.Throws<ArgumentException>(
            () => Forecaster.Run(series, months, new ForecastOptions()));
    }

    // ================================================================================
    //  Core happy path over a 36-month seasonal+trend series
    // ================================================================================

    [Fact]
    public void Run_SeasonalTrend_CoreShapeAndInvariants()
    {
        var (series, months) = Build(36); // 2023-01 .. 2025-12
        var opt = new ForecastOptions(Horizon: 6, Holdout: 6);

        var r = Forecaster.Run(series, months, opt);

        // Holdout is min(6, n-12=24) then clamped => 6.
        Assert.Equal(6, r.Holdout);
        Assert.Equal(6, r.Horizon);
        Assert.Equal(36, r.TotalN);
        Assert.Equal(30, r.TrainN);
        Assert.Equal("2025-12", r.LastMonth);

        // Counts.
        Assert.Equal(6, r.Backtest.Count);
        Assert.Equal(6, r.Future.Count);
        Assert.Equal(series.Length, r.History.Count);

        // History values + hold flags: last `holdout` are IsHold, the rest are not.
        for (var i = 0; i < r.History.Count; i++)
        {
            Assert.Equal(months[i], r.History[i].Month);
            Assert.Equal(series[i], r.History[i].Value, 6);
            Assert.Equal(i >= series.Length - r.Holdout, r.History[i].IsHold);
        }

        Assert.All(r.History.Take(30), p => Assert.False(p.IsHold));
        Assert.All(r.History.Skip(30), p => Assert.True(p.IsHold));

        // HistUnits is the total of the raw series.
        Assert.Equal(Statistics.Sum(series), r.HistUnits, 6);

        // Sigma is a non-negative residual std, and > 0 given the non-periodic wiggle.
        Assert.True(r.Sigma > 0);

        // Future: non-negative values, non-negative bands, Hi >= Lo,
        // and band width is non-decreasing across the horizon.
        var prevWidth = double.NegativeInfinity;
        var expectedMonth = r.LastMonth;
        for (var k = 0; k < r.Future.Count; k++)
        {
            var f = r.Future[k];
            expectedMonth = MonthKey.Add(expectedMonth, 1);
            Assert.Equal(expectedMonth, f.Month); // sequential months after LastMonth
            Assert.True(f.Value >= 0);
            Assert.True(f.Lo >= 0);
            Assert.True(f.Hi >= f.Lo);

            var width = f.Hi - f.Lo;
            Assert.True(width >= prevWidth - 1e-6, $"band width shrank at k={k}");
            prevWidth = width;
        }

        // FutureSum is exactly the sum of the future point values.
        Assert.Equal(r.Future.Sum(f => f.Value), r.FutureSum, 6);

        // Backtest actuals are the holdout slice of the series; months line up.
        for (var i = 0; i < r.Backtest.Count; i++)
        {
            var idx = series.Length - r.Holdout + i;
            Assert.Equal(months[idx], r.Backtest[i].Month);
            Assert.Equal(series[idx], r.Backtest[i].Actual, 6);
            Assert.True(r.Backtest[i].Forecast >= 0);
        }

        // Methods: all four keys, exactly one IsBest and one IsChosen.
        Assert.Equal(4, r.Methods.Count);
        Assert.Equal(new[] { "naive", "ma3", "snaive", "hw" }, r.Methods.Select(m => m.Key).ToArray());
        Assert.Equal(1, r.Methods.Count(m => m.IsBest));
        Assert.Equal(1, r.Methods.Count(m => m.IsChosen));
        Assert.Equal(r.Best, r.Methods.Single(m => m.IsBest).Key);
        Assert.Equal(r.Chosen, r.Methods.Single(m => m.IsChosen).Key);

        // No algo override => the chosen model is the best model.
        Assert.Null(opt.Algo);
        Assert.Equal(r.Best, r.Chosen);

        // Explanation always has at least three points.
        Assert.True(r.Explanation.Points.Count >= 3);
    }

    // ================================================================================
    //  Algo override respected for every valid key
    // ================================================================================

    [Theory]
    [InlineData("naive", "last month")]
    [InlineData("ma3", "moving average")]
    [InlineData("snaive", "last year")]
    [InlineData("hw", "Holt-Winters")]
    public void Run_ValidAlgoOverride_IsRespected(string algo, string nameFragment)
    {
        var (series, months) = Build(36);

        var r = Forecaster.Run(series, months, new ForecastOptions(Algo: algo));

        Assert.Equal(algo, r.Chosen);
        Assert.Contains(nameFragment, r.ChosenName);
        Assert.Equal(1, r.Methods.Count(m => m.IsChosen));
        Assert.Equal(algo, r.Methods.Single(m => m.IsChosen).Key);

        // Best is still whichever won the back-test, and stays a single flag.
        Assert.Equal(1, r.Methods.Count(m => m.IsBest));

        // Structural invariants hold regardless of override.
        Assert.All(r.Future, f => Assert.True(f.Value >= 0 && f.Lo >= 0 && f.Hi >= f.Lo));
    }

    [Fact]
    public void Run_InvalidAlgoOverride_FallsBackToBest()
    {
        var (series, months) = Build(36);

        var r = Forecaster.Run(series, months, new ForecastOptions(Algo: "not-a-real-model"));

        // Unknown key is ignored => chosen == best.
        Assert.Equal(r.Best, r.Chosen);
    }

    // ---- Deterministic forecast values for the transparent overrides ----------------

    [Fact]
    public void Run_NaiveOverride_ProjectsLastValueAndLastTrainValue()
    {
        var (series, months) = Build(36);
        var n = series.Length;
        var r = Forecaster.Run(series, months, new ForecastOptions(Algo: "naive"));

        // Naive future = last observed value (clamped >= 0), repeated across the horizon.
        var last = series[n - 1];
        Assert.All(r.Future, f => Assert.Equal(last, f.Value, 6));
        Assert.Equal(r.Horizon * last, r.FutureSum, 6);

        // Naive back-test forecast = last TRAIN value (index n - hold - 1).
        var lastTrain = series[n - r.Holdout - 1];
        Assert.All(r.Backtest, b => Assert.Equal(lastTrain, b.Forecast, 6));

        // Trend is up, so the projection reads as a continuation/uplift.
        Assert.Contains(r.Explanation.Points, p => p.Contains("continuation/uplift"));
    }

    [Fact]
    public void Run_MovingAvgOverride_ProjectsMeanOfLastThree()
    {
        var (series, months) = Build(36);
        var n = series.Length;
        var r = Forecaster.Run(series, months, new ForecastOptions(Algo: "ma3"));

        // Future = mean of the last 3 observed values (clamped >= 0).
        var expectedFuture = Math.Max(0, MeanRange(series, n - 3, 3));
        Assert.All(r.Future, f => Assert.Equal(expectedFuture, f.Value, 6));

        // Back-test = mean of the last 3 TRAIN values.
        var trainN = n - r.Holdout;
        var expectedBt = Math.Max(0, MeanRange(series, trainN - 3, 3));
        Assert.All(r.Backtest, b => Assert.Equal(expectedBt, b.Forecast, 6));
    }

    [Fact]
    public void Run_SeasonalNaiveOverride_ProjectsSamePeriodLastYear()
    {
        var (series, months) = Build(36);
        var n = series.Length;
        var r = Forecaster.Run(series, months, new ForecastOptions(Algo: "snaive", Horizon: 6));

        // Seasonal naive future[k] = y[n - 12 + k] for k = 0..h-1 (h <= 12).
        for (var k = 0; k < r.Future.Count; k++)
        {
            Assert.Equal(Math.Max(0, series[n - 12 + k]), r.Future[k].Value, 6);
        }

        // Back-test uses the train series (length n - hold): fc[k] = trainY[trainN - 12 + k].
        var trainN = n - r.Holdout;
        for (var k = 0; k < r.Backtest.Count; k++)
        {
            Assert.Equal(Math.Max(0, series[trainN - 12 + k]), r.Backtest[k].Forecast, 6);
        }
    }

    // ================================================================================
    //  Confidence-interval width scales with the CI level
    // ================================================================================

    [Fact]
    public void Run_HigherCi_ProducesWiderBands()
    {
        var (series, months) = Build(36);

        var r80 = Forecaster.Run(series, months, new ForecastOptions(Ci: 80));
        var r90 = Forecaster.Run(series, months, new ForecastOptions(Ci: 90));
        var r95 = Forecaster.Run(series, months, new ForecastOptions(Ci: 95));

        // CI only changes the z multiplier: chosen model, sigma and point forecasts are
        // identical, so the upper band strictly widens 80 < 90 < 95 at every horizon.
        for (var k = 0; k < r80.Future.Count; k++)
        {
            Assert.Equal(r80.Future[k].Value, r95.Future[k].Value, 6);

            Assert.True(r95.Future[k].Hi > r90.Future[k].Hi);
            Assert.True(r90.Future[k].Hi > r80.Future[k].Hi);

            // Lower band moves the other way (never above the smaller-CI Lo).
            Assert.True(r95.Future[k].Lo <= r80.Future[k].Lo + 1e-9);

            // Overall band width is wider at 95 than at 80.
            Assert.True((r95.Future[k].Hi - r95.Future[k].Lo) >= (r80.Future[k].Hi - r80.Future[k].Lo));
        }
    }

    // ================================================================================
    //  Holdout clamping & method fallbacks on short series
    // ================================================================================

    [Fact]
    public void Run_ShortSeries_ClampsHoldoutAndUsesFallbacks()
    {
        // n <= 12 => the (n-12>0 ? n-12 : 6) branch takes the 6.
        var (series, months) = Build(8);

        var r = Forecaster.Run(series, months, new ForecastOptions(Horizon: 4, Holdout: 6));

        Assert.Equal(6, r.Holdout);            // min(6, 6) then clamped to <= n-1 = 7 => 6
        Assert.Equal(8, r.History.Count);
        Assert.Equal(6, r.Backtest.Count);
        Assert.Equal(4, r.Future.Count);
        Assert.Equal(2, r.TrainN);             // n - hold

        // snaive falls back to naive and hw to Holt-linear (train len < 12), but the run
        // still succeeds and every method still appears.
        Assert.Equal(4, r.Methods.Count);
        Assert.All(r.Future, f => Assert.True(f.Value >= 0 && f.Lo >= 0 && f.Hi >= f.Lo));
    }

    [Fact]
    public void Run_MinimalSeries_ClampsHoldoutToOne()
    {
        var (series, months) = Build(2);

        var r = Forecaster.Run(series, months, new ForecastOptions(Horizon: 3, Holdout: 6));

        Assert.Equal(1, r.Holdout);            // clamped to min(hold, n-1) = 1
        Assert.Equal(2, r.History.Count);
        Assert.Single(r.Backtest);
        Assert.Equal(3, r.Future.Count);
        Assert.False(r.History[0].IsHold);
        Assert.True(r.History[1].IsHold);
    }

    // ================================================================================
    //  Explanation — demand-trend narrative branches
    // ================================================================================

    private static ForecastResult RunPlateau(double baseLevel, double recentLevel, double? lift = null)
    {
        // 20-month series: everything at `baseLevel` except the last 3 months at
        // `recentLevel`. recent3 = mean(last 3); prior12 = mean(indices 5..16).
        const int n = 20;
        var series = new double[n];
        for (var i = 0; i < n; i++)
        {
            series[i] = i >= n - 3 ? recentLevel : baseLevel;
        }

        return Forecaster.Run(series, Months(n), new ForecastOptions(), lift);
    }

    [Fact]
    public void Explain_RisingRecentDemand_SaysAbove()
    {
        var r = RunPlateau(baseLevel: 100, recentLevel: 200);

        Assert.Equal(200, r.Explanation.Recent3, 6);
        Assert.Equal(100, r.Explanation.Prior12, 6);
        Assert.Equal(100, r.Explanation.ChangePct, 6); // (200-100)/100*100
        Assert.Contains("above", r.Explanation.Points[0]);
    }

    [Fact]
    public void Explain_FallingRecentDemand_SaysSlowed()
    {
        var r = RunPlateau(baseLevel: 200, recentLevel: 100);

        Assert.Equal(100, r.Explanation.Recent3, 6);
        Assert.Equal(200, r.Explanation.Prior12, 6);
        Assert.Equal(-50, r.Explanation.ChangePct, 6);
        Assert.Contains("slowed", r.Explanation.Points[0]);
    }

    [Fact]
    public void Explain_StableDemand_SaysStable()
    {
        var r = RunPlateau(baseLevel: 150, recentLevel: 150);

        Assert.Equal(0, r.Explanation.ChangePct, 6);
        Assert.Contains("stable", r.Explanation.Points[0]);
    }

    [Fact]
    public void Explain_ZeroPriorAverage_TreatedAsStable()
    {
        // prior12 window is entirely zero => the change-pct guard yields 0 => stable,
        // even though recent demand is non-zero.
        var r = RunPlateau(baseLevel: 0, recentLevel: 50);

        Assert.Equal(0, r.Explanation.Prior12, 6);
        Assert.Equal(0, r.Explanation.ChangePct, 6);
        Assert.Contains("stable", r.Explanation.Points[0]);
    }

    [Fact]
    public void Explain_LastMonthDipsBelowRecentMean_SaysModeration()
    {
        // Last 3 months = 180,180,120 => recent3 = 160; naive projects 120 < 160,
        // so the model narrative reads as a moderation.
        const int n = 20;
        var series = new double[n];
        for (var i = 0; i < n; i++)
        {
            series[i] = 150;
        }

        series[17] = 180;
        series[18] = 180;
        series[19] = 120;

        var r = Forecaster.Run(series, Months(n), new ForecastOptions(Algo: "naive"));

        Assert.Equal(160, r.Explanation.Recent3, 6);
        Assert.Contains(r.Explanation.Points, p => p.Contains("moderation"));
    }

    // ---- Ramadan association line ---------------------------------------------------

    [Fact]
    public void Explain_PositiveRamadanLift_AddsAssociationPoint()
    {
        var (series, months) = Build(36);

        var r = Forecaster.Run(series, months, new ForecastOptions(), ramadanLiftPct: 20);

        var point = Assert.Single(r.Explanation.Points, p => p.Contains("Ramadan"));
        Assert.Contains("+20", point);
        Assert.True(r.Explanation.Points.Count >= 4);
    }

    [Fact]
    public void Explain_NegativeRamadanLift_AddsAssociationPointWithoutPlusSign()
    {
        var (series, months) = Build(36);

        var r = Forecaster.Run(series, months, new ForecastOptions(), ramadanLiftPct: -15);

        var point = Assert.Single(r.Explanation.Points, p => p.Contains("Ramadan"));
        Assert.Contains("-15", point);
        Assert.DoesNotContain("+", point);
    }

    [Fact]
    public void Explain_SmallRamadanLift_IsOmitted()
    {
        var (series, months) = Build(36);

        var r = Forecaster.Run(series, months, new ForecastOptions(), ramadanLiftPct: 3);

        Assert.DoesNotContain(r.Explanation.Points, p => p.Contains("Ramadan"));
        var pointCount = r.Explanation.Points.Count; // trend + direction + wmape only
        Assert.Equal(3, pointCount);
    }

    [Fact]
    public void Explain_NoRamadanLift_IsOmitted()
    {
        var (series, months) = Build(36);

        var r = Forecaster.Run(series, months, new ForecastOptions());

        Assert.DoesNotContain(r.Explanation.Points, p => p.Contains("Ramadan"));
    }

    // ---- Confidence wording keys off back-test WMAPE --------------------------------

    private static ForecastResult RunTrainThenHoldout(double trainLevel, double holdoutLevel)
    {
        // 20 months: first 14 at trainLevel, last 6 at holdoutLevel. The holdout is 6,
        // so every method forecasts `trainLevel` and the WMAPE is deterministic.
        const int n = 20;
        var series = new double[n];
        for (var i = 0; i < n; i++)
        {
            series[i] = i < 14 ? trainLevel : holdoutLevel;
        }

        return Forecaster.Run(series, Months(n), new ForecastOptions());
    }

    [Fact]
    public void Explain_PerfectBacktest_ReportsHighConfidence()
    {
        var r = RunTrainThenHoldout(trainLevel: 100, holdoutLevel: 100); // WMAPE 0%

        var last = r.Explanation.Points[^1];
        Assert.Contains("0.0%", last);
        Assert.Contains("high", last);
    }

    [Fact]
    public void Explain_ModerateBacktest_ReportsMediumConfidence()
    {
        var r = RunTrainThenHoldout(trainLevel: 100, holdoutLevel: 80); // WMAPE 25%

        var last = r.Explanation.Points[^1];
        Assert.Contains("25.0%", last);
        Assert.Contains("medium", last);
    }

    [Fact]
    public void Explain_PoorBacktest_ReportsLowConfidence()
    {
        var r = RunTrainThenHoldout(trainLevel: 100, holdoutLevel: 60); // WMAPE ~66.7%

        var last = r.Explanation.Points[^1];
        Assert.Contains("66.7%", last);
        Assert.Contains("low", last);
    }

    [Fact]
    public void Run_AllZeroHoldout_NullWmapeYieldsLowConfidenceAndHwFallback()
    {
        // Holdout entirely zero => every method's WMAPE is null => best falls back to "hw".
        const int n = 20;
        var series = new double[n];
        for (var i = 0; i < n; i++)
        {
            series[i] = i < 14 ? 100 : 0;
        }

        var r = Forecaster.Run(series, Months(n), new ForecastOptions());

        Assert.Equal("hw", r.Best);
        Assert.Equal("hw", r.Chosen);
        Assert.Null(r.Accuracy.Wmape);

        var last = r.Explanation.Points[^1];
        Assert.Contains("n/a", last);
        Assert.Contains("low", last);

        // Every candidate reports a null WMAPE in the comparison table.
        Assert.All(r.Methods, m => Assert.Null(m.Wmape));
    }
}
