using System.Linq;

using BeeEye.Analytics;
using BeeEye.Analytics.Forecasting;

using Xunit;

namespace BeeEye.Analytics.Tests;

/// <summary>
/// Unit tests for <see cref="ForecastMethods"/>, the transparent baseline forecasters
/// ported from docs/wireframes/engine.js.
/// </summary>
public sealed class ForecastMethodsTests
{
    // Exact method names as emitted by the source (note the diaeresis on the "i").
    private const string NaiveName = "Naïve (last month)";                 // "Naïve (last month)"
    private const string SeasonalNaiveName = "Seasonal naïve (last year)"; // "Seasonal naïve (last year)"
    private const string HoltName = "Holt";
    private const string HoltWintersName = "Holt-Winters";

    private static void AssertSequence(IReadOnlyList<double> expected, IReadOnlyList<double> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i], actual[i], 6);
        }
    }

    // ------------------------------------------------------------------ Naive

    [Fact]
    public void Naive_NonEmpty_ForecastIsAllLastValue_AndFittedIsShifted()
    {
        var y = new double[] { 10, 20, 30 };

        var r = ForecastMethods.Naive(y, 4);

        Assert.Equal(NaiveName, r.Name);
        // fc is filled with the last observed value.
        Assert.Equal(4, r.Fc.Count);
        Assert.All(r.Fc, v => Assert.Equal(30, v, 6));
        // fitted[i] == y[i-1] for i > 0, fitted[0] == y[0].
        AssertSequence(new double[] { 10, 10, 20 }, r.Fitted);
    }

    [Fact]
    public void Naive_Single_FittedIsFirstValue_AndForecastRepeatsIt()
    {
        var y = new double[] { 7 };

        var r = ForecastMethods.Naive(y, 2);

        AssertSequence(new double[] { 7 }, r.Fitted);
        AssertSequence(new double[] { 7, 7 }, r.Fc);
        Assert.Equal(NaiveName, r.Name);
    }

    [Fact]
    public void Naive_Empty_ReturnsEmptyFitted_AndZeroForecast()
    {
        var y = System.Array.Empty<double>();

        var r = ForecastMethods.Naive(y, 3);

        Assert.Empty(r.Fitted);
        Assert.Equal(3, r.Fc.Count);
        Assert.All(r.Fc, v => Assert.Equal(0, v, 6));
        Assert.Equal(NaiveName, r.Name);
    }

    [Fact]
    public void Naive_ZeroHorizon_ProducesEmptyForecast()
    {
        var y = new double[] { 5, 6 };

        var r = ForecastMethods.Naive(y, 0);

        Assert.Empty(r.Fc);
        AssertSequence(new double[] { 5, 5 }, r.Fitted);
    }

    // -------------------------------------------------------------- MovingAvg

    [Fact]
    public void MovingAvg_DefaultK_ForecastIsMeanOfLastThree_AndFittedFollowsRule()
    {
        var y = new double[] { 2, 4, 6, 8, 10 };

        var r = ForecastMethods.MovingAvg(y, 2); // k defaults to 3

        Assert.Equal("3-month moving average", r.Name);
        // v = mean(last 3) = mean(6,8,10) = 8.
        Assert.Equal(2, r.Fc.Count);
        Assert.All(r.Fc, v => Assert.Equal(8, v, 6));
        // fitted: i<k expanding mean of [0..i]; i>=k mean of the k values BEFORE i (exclusive).
        AssertSequence(new double[] { 2, 3, 4, 4, 6 }, r.Fitted);
    }

    [Fact]
    public void MovingAvg_CustomK_UsesThatWindow()
    {
        var y = new double[] { 1, 2, 3, 4 };

        var r = ForecastMethods.MovingAvg(y, 1, k: 2);

        Assert.Equal("2-month moving average", r.Name);
        // v = mean(last 2) = mean(3,4) = 3.5.
        AssertSequence(new double[] { 3.5 }, r.Fc);
        // fitted: i<2 expanding; i>=2 mean of the 2 values before i.
        AssertSequence(new double[] { 1, 1.5, 1.5, 2.5 }, r.Fitted);
    }

    [Fact]
    public void MovingAvg_NegativeMean_ForecastClampedToZero()
    {
        var y = new double[] { -10, -20, -30 };

        var r = ForecastMethods.MovingAvg(y, 3); // k=3, v = mean(-10,-20,-30) = -20

        Assert.All(r.Fc, v => Assert.Equal(0, v, 6)); // Math.Max(0, -20) == 0
        Assert.Equal(3, r.Fc.Count);
    }

    [Fact]
    public void MovingAvg_WindowLargerThanSeries_IsClampedBySlice()
    {
        var y = new double[] { 5, 10 };

        var r = ForecastMethods.MovingAvg(y, 1, k: 3);

        // last-k slice clamps to the whole series: mean(5,10) = 7.5.
        AssertSequence(new double[] { 7.5 }, r.Fc);
        // fitted: both indices are < k so expanding means.
        AssertSequence(new double[] { 5, 7.5 }, r.Fitted);
    }

    // ----------------------------------------------------------- SeasonalNaive

    [Fact]
    public void SeasonalNaive_ShortSeries_FallsBackToNaive()
    {
        var y = new double[] { 3, 6, 9, 12, 15 }; // Count 5 < default m 12

        var r = ForecastMethods.SeasonalNaive(y, 4);

        // Delegates to Naive: name and behavior match Naive exactly.
        Assert.Equal(NaiveName, r.Name);
        Assert.All(r.Fc, v => Assert.Equal(15, v, 6));
        AssertSequence(new double[] { 3, 3, 6, 9, 12 }, r.Fitted);
    }

    [Fact]
    public void SeasonalNaive_SmallPeriod_ForecastWrapsAndFittedShiftsByPeriod()
    {
        var y = new double[] { 1, 2, 3, 4, 5, 6 }; // n=6, m=3

        var r = ForecastMethods.SeasonalNaive(y, 5, m: 3);

        Assert.Equal(SeasonalNaiveName, r.Name);
        // fc[k-1] = y[n-m + ((k-1) % m)] = y[3 + ((k-1)%3)].
        AssertSequence(new double[] { 4, 5, 6, 4, 5 }, r.Fc);
        // fitted[i] = i>=m ? y[i-m] : y[i].
        AssertSequence(new double[] { 1, 2, 3, 1, 2, 3 }, r.Fitted);
    }

    [Fact]
    public void SeasonalNaive_NegativeSeasonalValues_ForecastClampedToZero()
    {
        var y = new double[] { -1, -2, -3, -4, -5, -6 }; // n=6, m=3

        var r = ForecastMethods.SeasonalNaive(y, 3, m: 3);

        Assert.All(r.Fc, v => Assert.Equal(0, v, 6)); // each Math.Max(0, negative) == 0
        Assert.Equal(3, r.Fc.Count);
    }

    [Fact]
    public void SeasonalNaive_TwelveMonthPeriod_ForecastUsesLastYearAndWraps()
    {
        // 24 months, values 1..24, default m = 12.
        var y = Enumerable.Range(1, 24).Select(i => (double)i).ToArray();

        var r = ForecastMethods.SeasonalNaive(y, 14); // h > m to exercise wraparound

        Assert.Equal(SeasonalNaiveName, r.Name);
        // n-m = 12 -> fc[k-1] = y[12 + ((k-1) % 12)].
        var expectedFc = new double[]
        {
            13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, // one full year (last season)
            13, 14,                                          // wraps to start of last season
        };
        AssertSequence(expectedFc, r.Fc);
        // fitted[i] = i>=12 ? y[i-12] : y[i]  => first 12 unchanged, next 12 == first 12.
        var expectedFitted = Enumerable.Range(1, 12).Concat(Enumerable.Range(1, 12))
            .Select(i => (double)i).ToArray();
        AssertSequence(expectedFitted, r.Fitted);
    }

    // ------------------------------------------------------------- HoltLinear

    [Fact]
    public void HoltLinear_Empty_ReturnsEmptyFittedAndZeroForecast()
    {
        var y = System.Array.Empty<double>();

        var r = ForecastMethods.HoltLinear(y, 3);

        Assert.Equal(HoltName, r.Name);
        Assert.Empty(r.Fitted);
        Assert.Equal(3, r.Fc.Count);
        Assert.All(r.Fc, v => Assert.Equal(0, v, 6));
    }

    [Fact]
    public void HoltLinear_Single_HasZeroTrend_ConstantForecast()
    {
        var y = new double[] { 5 };

        var r = ForecastMethods.HoltLinear(y, 3);

        // n==1 -> trend 0, level 5; fc all Max(0, 5 + k*0) == 5.
        AssertSequence(new double[] { 5 }, r.Fitted);
        AssertSequence(new double[] { 5, 5, 5 }, r.Fc);
        Assert.Equal(HoltName, r.Name);
    }

    [Fact]
    public void HoltLinear_SteepDownTrend_ForecastClampedToZero()
    {
        var y = new double[] { 100, 50 };

        var r = ForecastMethods.HoltLinear(y, 3); // alpha 0.4, beta 0.1

        // fitted[0] == y[0]; fitted[1] == level+trend == y[1].
        AssertSequence(new double[] { 100, 50 }, r.Fitted);
        // level ends at 50, trend at -50 -> every forecast is Max(0, negative) == 0.
        Assert.All(r.Fc, v => Assert.Equal(0, v, 6));
        Assert.Equal(3, r.Fc.Count);
    }

    [Fact]
    public void HoltLinear_Invariants_LengthsNonNegativityAndInitialFit()
    {
        var y = new double[] { 10, 12, 15, 14, 18, 20 };

        var r = ForecastMethods.HoltLinear(y, 4);

        Assert.Equal(HoltName, r.Name);
        Assert.Equal(y.Length, r.Fitted.Count); // fitted length == n
        Assert.Equal(4, r.Fc.Count);            // fc length == h
        Assert.All(r.Fc, v => Assert.True(v >= 0, "forecasts are Math.Max(0, ...)"));
        // Deterministic initial fit regardless of alpha/beta.
        Assert.Equal(10, r.Fitted[0], 6);       // fitted[0] == y[0]
        Assert.Equal(12, r.Fitted[1], 6);       // fitted[1] == level + trend == y[1]
    }

    // ------------------------------------------------------------ HoltWinters

    [Fact]
    public void HoltWinters_ShortSeries_DelegatesToHoltLinear()
    {
        // n = 13 < m + 2 = 14 -> falls back to Holt (linear).
        var y = Enumerable.Range(1, 13).Select(i => (double)i).ToArray();

        var r = ForecastMethods.HoltWinters(y, m: 12, h: 4, alpha: 0.4, beta: 0.1, gamma: 0.2);

        Assert.Equal(HoltName, r.Name); // proves delegation to HoltLinear
        Assert.Equal(y.Length, r.Fitted.Count);
        Assert.Equal(4, r.Fc.Count);
        Assert.All(r.Fc, v => Assert.True(v >= 0));
    }

    [Fact]
    public void HoltWinters_BoundaryLength_UsesFullSeasonalPath()
    {
        // n = 14 == m + 2 -> full Holt-Winters path (seasons = 14/12 = 1).
        var y = BuildSeasonalSeries(14, m: 12);

        var r = ForecastMethods.HoltWinters(y, m: 12, h: 6, alpha: 0.4, beta: 0.1, gamma: 0.2);

        Assert.Equal(HoltWintersName, r.Name);
        Assert.Equal(y.Length, r.Fitted.Count);
        Assert.Equal(6, r.Fc.Count);
        Assert.All(r.Fc, v => Assert.True(v >= 0));
    }

    [Fact]
    public void HoltWinters_TwoFullSeasons_ProducesWellFormedOutput()
    {
        var y = BuildSeasonalSeries(24, m: 12); // exactly 2 seasons

        var r = ForecastMethods.HoltWinters(y, m: 12, h: 15, alpha: 0.4, beta: 0.1, gamma: 0.2);

        Assert.Equal(HoltWintersName, r.Name);
        Assert.Equal(24, r.Fitted.Count);                 // fitted length == n
        Assert.Equal(15, r.Fc.Count);                     // fc length == h (h > m exercises wraparound)
        Assert.All(r.Fc, v => Assert.True(v >= 0));        // Math.Max(0, ...)
        Assert.All(r.Fc, v => Assert.True(double.IsFinite(v)));
        Assert.All(r.Fitted, v => Assert.True(double.IsFinite(v)));
    }

    [Fact]
    public void HoltWinters_ThreeFullSeasons_ProducesWellFormedOutput()
    {
        var y = BuildSeasonalSeries(36, m: 12); // 3 seasons

        var r = ForecastMethods.HoltWinters(y, m: 12, h: 12, alpha: 0.5, beta: 0.2, gamma: 0.3);

        Assert.Equal(HoltWintersName, r.Name);
        Assert.Equal(36, r.Fitted.Count);
        Assert.Equal(12, r.Fc.Count);
        Assert.All(r.Fc, v => Assert.True(v >= 0));
        Assert.All(r.Fc, v => Assert.True(double.IsFinite(v)));
    }

    /// <summary>
    /// Deterministic, strictly-positive seasonal series with a mild upward trend:
    /// value[i] = 200 + i + season[i % m].
    /// </summary>
    private static double[] BuildSeasonalSeries(int count, int m)
    {
        var season = new double[] { 20, 40, 60, 80, 100, 120, 100, 80, 60, 40, 20, 10 };
        var y = new double[count];
        for (var i = 0; i < count; i++)
        {
            y[i] = 200 + i + season[i % m];
        }

        return y;
    }
}
