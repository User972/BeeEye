using System.Collections.Generic;
using BeeEye.Analytics.SpareParts;
using Xunit;

namespace BeeEye.Analytics.Tests;

/// <summary>
/// Deterministic unit tests for the UC7 intermittent-demand methods.
///
/// Canonical Croston example: series [0,0,5,0,0,5], α=0.1.
///   first demand at index 2 -> level=5, interval=3 (periods from start).
///   second demand: since_last=3 -> level=5+0.1(5-5)=5; interval=3+0.1(3-3)=3.
///   rate = 5/3 = 1.66666667 ; SBA = (1-0.05)*1.66667 = 1.58333333.
///
/// Canonical TSB example: series [0,0,5,0,0,5], αp=αz=0.1.
///   p0 = 2/6 = 0.33333, z0 = 5.
///   0:0.3 ; 0:0.27 ; 5:p=0.343,z=5 ; 0:0.3087 ; 0:0.27783 ; 5:p=0.350047,z=5.
///   rate = 0.350047 * 5 = 1.750235.
/// </summary>
public class IntermittentTests
{
    private static readonly double[] Canonical = [0, 0, 5, 0, 0, 5];

    [Fact]
    public void Croston_CanonicalSeries()
    {
        Assert.Equal(1.66666667, Intermittent.Croston(Canonical, 0.1), 6);
    }

    [Fact]
    public void Croston_TwoDemandsWithSmoothing()
    {
        // [2,0,0,3], α=0.5: level=2,interval=1 then level=2+0.5(3-2)=2.5, interval=1+0.5(3-1)=2 -> 2.5/2 = 1.25
        Assert.Equal(1.25, Intermittent.Croston([2, 0, 0, 3], 0.5), 10);
    }

    [Fact]
    public void Croston_AllZeros_IsZero()
    {
        Assert.Equal(0.0, Intermittent.Croston([0, 0, 0, 0], 0.1), 10);
        Assert.Equal(0.0, Intermittent.Croston([], 0.1), 10);
    }

    [Fact]
    public void Sba_IsCrostonScaledByBiasCorrection()
    {
        Assert.Equal(1.58333333, Intermittent.Sba(Canonical, 0.1), 6);
        // SBA < Croston for the same series (the deliberate over-forecast correction).
        Assert.True(Intermittent.Sba(Canonical, 0.1) < Intermittent.Croston(Canonical, 0.1));
    }

    [Fact]
    public void Tsb_CanonicalSeries()
    {
        Assert.Equal(1.750235, Intermittent.Tsb(Canonical, 0.1, 0.1), 5);
    }

    [Fact]
    public void Tsb_DecaysOnTrailingZeros()
    {
        // A part with demand early then a long zero run: TSB probability decays, so the later estimate is lower.
        double[] early = [5, 5, 5, 0, 0, 0];
        double[] late = [0, 0, 0, 5, 5, 5];
        Assert.True(Intermittent.Tsb(early, 0.2, 0.2) < Intermittent.Tsb(late, 0.2, 0.2));
    }

    [Fact]
    public void Tsb_AllZeros_IsZero()
    {
        Assert.Equal(0.0, Intermittent.Tsb([0, 0, 0], 0.1, 0.1), 10);
    }

    [Fact]
    public void Ses_TracksLevel()
    {
        // [10,10,10] -> stays 10 regardless of alpha.
        Assert.Equal(10.0, Intermittent.Ses([10, 10, 10], 0.3), 10);
        // [0,10] with alpha 0.5 -> 0 + 0.5*(10-0) = 5
        Assert.Equal(5.0, Intermittent.Ses([0, 10], 0.5), 10);
        Assert.Equal(0.0, Intermittent.Ses([], 0.5), 10);
    }

    [Fact]
    public void Adi_And_Cv2()
    {
        Assert.Equal(3.0, Intermittent.Adi(Canonical)!.Value, 10); // 6 periods / 2 demands
        Assert.Equal(0.0, Intermittent.Cv2(Canonical), 10);        // equal sizes -> no variability
        Assert.Null(Intermittent.Adi([0, 0, 0]));                  // no demand -> null
        Assert.Null(Intermittent.Adi([]));
    }

    [Fact]
    public void Cv2_VariableSizes_IsPositive()
    {
        // nz sizes [2,25,10]: cv^2 well above the 0.49 lumpy threshold.
        Assert.True(Intermittent.Cv2([0, 0, 2, 0, 0, 0, 25, 0, 0, 10, 0, 0]) >= 0.49);
    }

    [Theory]
    // Smooth: frequent (ADI 1.0) and low variability.
    [InlineData(new[] { 10.0, 11, 9, 10, 12, 10, 11, 9 }, DemandClass.Smooth)]
    // Erratic: frequent but highly variable sizes.
    [InlineData(new[] { 1.0, 20, 2, 15, 3, 25, 1, 18 }, DemandClass.Erratic)]
    // Intermittent: infrequent (ADI 3.0), stable size.
    [InlineData(new[] { 0.0, 0, 5, 0, 0, 5, 0, 0, 5, 0, 0, 5 }, DemandClass.Intermittent)]
    // Lumpy: infrequent and variable.
    [InlineData(new[] { 0.0, 0, 2, 0, 0, 0, 25, 0, 0, 10, 0, 0 }, DemandClass.Lumpy)]
    // Obsolescent: demand early, none in the second half.
    [InlineData(new[] { 5.0, 0, 4, 0, 6, 0, 0, 0, 0, 0, 0, 0 }, DemandClass.Obsolescent)]
    public void Classify_AssignsExpectedClass(double[] series, DemandClass expected)
    {
        Assert.Equal(expected, Intermittent.Classify(series, SparePartsSettings.Default));
    }

    [Fact]
    public void Classify_TooFewMonths_IsInsufficient()
    {
        Assert.Equal(DemandClass.InsufficientData, Intermittent.Classify([0, 0, 1, 0], SparePartsSettings.Default));
    }

    [Fact]
    public void Classify_TooFewNonZero_IsInsufficient()
    {
        // 6 months but only one demand -> below MinNonZeroPeriods (2).
        Assert.Equal(DemandClass.InsufficientData, Intermittent.Classify([0, 0, 0, 5, 0, 0], SparePartsSettings.Default));
    }

    [Fact]
    public void MethodFor_And_RateFor_MatchTable()
    {
        var comparison = new MethodComparison(1, 2, 3, 4);
        Assert.Equal("SES", Intermittent.MethodFor(DemandClass.Smooth));
        Assert.Equal("SBA", Intermittent.MethodFor(DemandClass.Erratic));
        Assert.Equal("SBA", Intermittent.MethodFor(DemandClass.Intermittent));
        Assert.Equal("TSB", Intermittent.MethodFor(DemandClass.Lumpy));
        Assert.Equal("TSB", Intermittent.MethodFor(DemandClass.Obsolescent));
        Assert.Equal("None", Intermittent.MethodFor(DemandClass.InsufficientData));

        Assert.Equal(1, Intermittent.RateFor(DemandClass.Smooth, comparison), 10);
        Assert.Equal(3, Intermittent.RateFor(DemandClass.Intermittent, comparison), 10);
        Assert.Equal(4, Intermittent.RateFor(DemandClass.Lumpy, comparison), 10);
        Assert.Equal(0, Intermittent.RateFor(DemandClass.InsufficientData, comparison), 10);
    }

    [Fact]
    public void Forecast_Intermittent_UsesSbaRateAndNotInsufficient()
    {
        double[] series = [0, 0, 5, 0, 0, 5, 0, 0, 5, 0, 0, 5];
        var f = Intermittent.Forecast(series, SparePartsSettings.Default);
        Assert.Equal(DemandClass.Intermittent, f.Class);
        Assert.Equal("SBA", f.Method);
        Assert.False(f.InsufficientData);
        Assert.Equal(Intermittent.Sba(series, 0.1), f.RatePerPeriod, 10);
        Assert.True(f.RangeHigh >= f.RatePerPeriod);
        Assert.True(f.RangeLow <= f.RatePerPeriod);
        Assert.Equal(4, f.NonZeroPeriods);
        Assert.Equal(12, f.Periods);
    }

    [Fact]
    public void Forecast_InsufficientData_DoesNotFabricateAndFlags()
    {
        var f = Intermittent.Forecast([0, 0, 0, 5, 0, 0], SparePartsSettings.Default);
        Assert.True(f.InsufficientData);
        Assert.Equal(DemandClass.InsufficientData, f.Class);
        Assert.Equal("None", f.Method);
        Assert.Equal("Low", f.Confidence);
    }

    [Fact]
    public void Forecast_Comparison_ExposesAllFourMethods()
    {
        double[] series = [0, 0, 5, 0, 0, 5];
        var f = Intermittent.Forecast(series, SparePartsSettings.Default);
        Assert.Equal(Intermittent.Ses(series, 0.1), f.Comparison.Ses, 10);
        Assert.Equal(Intermittent.Croston(series, 0.1), f.Comparison.Croston, 10);
        Assert.Equal(Intermittent.Sba(series, 0.1), f.Comparison.Sba, 10);
        Assert.Equal(Intermittent.Tsb(series, 0.1, 0.1), f.Comparison.Tsb, 10);
    }

    [Fact]
    public void DenseSeries_FillsZerosOverMonthAxis()
    {
        var usage = new Dictionary<string, double> { ["2024-01"] = 3, ["2024-03"] = 5 };
        var dense = Intermittent.DenseSeries(usage, ["2024-01", "2024-02", "2024-03"]);
        Assert.Equal([3, 0, 5], dense);
    }

    [Theory]
    [InlineData(new[] { 1.0, 20, 2, 15, 3, 25, 1, 18 }, "SBA")]                 // Erratic
    [InlineData(new[] { 0.0, 0, 2, 0, 0, 0, 25, 0, 0, 10, 0, 0 }, "TSB")]        // Lumpy
    [InlineData(new[] { 5.0, 0, 4, 0, 6, 0, 0, 0, 0, 0, 0, 0 }, "TSB")]          // Obsolescent
    public void Forecast_AcrossClasses_ChoosesMethodAndBoundsRange(double[] series, string method)
    {
        var f = Intermittent.Forecast(series, SparePartsSettings.Default);
        Assert.False(f.InsufficientData);
        Assert.Equal(method, f.Method);
        Assert.True(f.RangeLow <= f.RatePerPeriod && f.RatePerPeriod <= f.RangeHigh);
        Assert.Contains(f.Confidence, new[] { "High", "Medium", "Low" });
    }

    [Fact]
    public void Cv2_SingleNonZero_IsZero()
    {
        Assert.Equal(0.0, Intermittent.Cv2([0, 0, 5, 0]), 10);
    }

    [Fact]
    public void Forecast_UsesDefaultSettingsWhenNull()
    {
        var f = Intermittent.Forecast([0, 0, 5, 0, 0, 5, 0, 0, 5, 0, 0, 5]);
        Assert.Equal(DemandClass.Intermittent, f.Class);
    }

    [Fact]
    public void IsObsolescent_RequiresDecliningSecondHalf()
    {
        Assert.True(Intermittent.IsObsolescent([5, 4, 6, 0, 0, 0], 0.5));
        Assert.False(Intermittent.IsObsolescent([0, 0, 0, 5, 4, 6], 0.5)); // rising, not declining
        Assert.False(Intermittent.IsObsolescent([1, 1], 0.5));             // too short
    }
}
