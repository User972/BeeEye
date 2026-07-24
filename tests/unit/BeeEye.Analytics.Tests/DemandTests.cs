using BeeEye.Analytics;
using BeeEye.Analytics.Demand;
using Xunit;

namespace BeeEye.Analytics.Tests;

/// <summary>
/// Tests for SalesRow, DemandAggregates.Build/accessors, and DemandCalculator
/// (Velocity fallback tiers + Trend), ported from engine.js.
/// </summary>
public class DemandTests
{
    private static SalesRow Row(string loc, string model, string variant, string month, double units)
        => new(loc, model, variant, month, units);

    // ----------------------------------------------------------------------
    // SalesRow record
    // ----------------------------------------------------------------------

    [Fact]
    public void SalesRow_DefaultsDiscountFlags()
    {
        var r = new SalesRow("L1", "M1", "V1", "2026-01", 5);
        Assert.Equal("L1", r.Location);
        Assert.Equal("M1", r.Model);
        Assert.Equal("V1", r.Variant);
        Assert.Equal("2026-01", r.MonthKey);
        Assert.Equal(5, r.Units, 6);
        Assert.False(r.Discounted);
        Assert.Equal(0, r.DiscountPct);
    }

    [Fact]
    public void SalesRow_ExplicitDiscountFlags()
    {
        var r = new SalesRow("L1", "M1", "V1", "2026-01", 5, true, 15);
        Assert.True(r.Discounted);
        Assert.Equal(15, r.DiscountPct);
    }

    [Fact]
    public void SalesRow_ValueEquality()
    {
        var a = new SalesRow("L1", "M1", "V1", "2026-01", 5);
        var b = new SalesRow("L1", "M1", "V1", "2026-01", 5);
        Assert.Equal(a, b);
    }

    // ----------------------------------------------------------------------
    // DemandAggregates.Build + accessors
    // ----------------------------------------------------------------------

    [Fact]
    public void Build_Empty_YieldsSentinelLastMonthAndZeroAccessors()
    {
        var agg = DemandAggregates.Build(Array.Empty<SalesRow>());

        Assert.Equal("0000-00", agg.LastMonth);
        Assert.Equal(0, agg.Lmv("L1", "M1", "V1", "2026-06"), 6);
        Assert.Equal(0, agg.Mv("M1", "V1", "2026-06"), 6);
        Assert.Equal(0, agg.Mdl("M1", "2026-06"), 6);
        Assert.Equal(0, agg.MvTot("M1", "V1"), 6);
        Assert.Equal(0, agg.LmvTot("L1", "M1", "V1"), 6);
        Assert.Equal(0, agg.ModelLocationCount("M1"));
    }

    private static DemandAggregates RichAgg() => DemandAggregates.Build(new[]
    {
        Row("L1", "M1", "V1", "2026-05", 10),
        Row("L1", "M1", "V1", "2026-06", 20),
        Row("L1", "M1", "V1", "2026-06", 5),   // duplicate key -> sums to 25
        Row("L2", "M1", "V1", "2026-06", 30),
        Row("L1", "M1", "V2", "2026-06", 7),
        Row("L3", "M2", "V1", "2026-04", 100),
    });

    [Fact]
    public void Build_ResolvesLastMonthAsOrdinalMax()
    {
        Assert.Equal("2026-06", RichAgg().LastMonth);
    }

    [Fact]
    public void Build_LastMonth_HandlesUnorderedInputAndDifferentYears()
    {
        var agg = DemandAggregates.Build(new[]
        {
            Row("L1", "M1", "V1", "2026-06", 1),
            Row("L1", "M1", "V1", "2025-01", 1),
            Row("L1", "M1", "V1", "2026-11", 1),
            Row("L1", "M1", "V1", "2026-02", 1),
        });
        Assert.Equal("2026-11", agg.LastMonth);
    }

    [Fact]
    public void Lmv_SumsDuplicateKeysAndKeepsMonthsSeparate()
    {
        var agg = RichAgg();
        Assert.Equal(25, agg.Lmv("L1", "M1", "V1", "2026-06"), 6); // 20 + 5
        Assert.Equal(10, agg.Lmv("L1", "M1", "V1", "2026-05"), 6);
        Assert.Equal(0, agg.Lmv("L1", "M1", "V1", "2026-04"), 6);
    }

    [Fact]
    public void Mv_SumsAcrossLocations()
    {
        var agg = RichAgg();
        Assert.Equal(55, agg.Mv("M1", "V1", "2026-06"), 6); // 25 (L1) + 30 (L2)
        Assert.Equal(7, agg.Mv("M1", "V2", "2026-06"), 6);
        Assert.Equal(0, agg.Mv("M1", "V1", "2026-05") - 10, 6); // L1 only -> 10
    }

    [Fact]
    public void Mdl_SumsAcrossVariantsAndLocations()
    {
        var agg = RichAgg();
        // 25 (L1 V1) + 30 (L2 V1) + 7 (L1 V2) = 62
        Assert.Equal(62, agg.Mdl("M1", "2026-06"), 6);
        Assert.Equal(100, agg.Mdl("M2", "2026-04"), 6);
    }

    [Fact]
    public void MvTot_SumsAcrossAllMonthsAndLocations()
    {
        var agg = RichAgg();
        // L1 V1: 10 + 20 + 5 = 35, plus L2 V1: 30 => 65
        Assert.Equal(65, agg.MvTot("M1", "V1"), 6);
        Assert.Equal(7, agg.MvTot("M1", "V2"), 6);
    }

    [Fact]
    public void LmvTot_SumsAcrossAllMonthsForOneLocation()
    {
        var agg = RichAgg();
        Assert.Equal(35, agg.LmvTot("L1", "M1", "V1"), 6); // 10 + 20 + 5
        Assert.Equal(30, agg.LmvTot("L2", "M1", "V1"), 6);
        Assert.Equal(0, agg.LmvTot("L9", "M1", "V1"), 6);
    }

    [Fact]
    public void ModelLocationCount_CountsDistinctLocations()
    {
        var agg = RichAgg();
        Assert.Equal(2, agg.ModelLocationCount("M1")); // L1, L2
        Assert.Equal(1, agg.ModelLocationCount("M2")); // L3
        Assert.Equal(0, agg.ModelLocationCount("Mx"));
    }

    // ----------------------------------------------------------------------
    // Velocity — Tier 1: Location-model-variant demand
    // ----------------------------------------------------------------------

    [Fact]
    public void Velocity_Tier1_HighConfidence_WhenTwoOrMoreNonZeroMonths()
    {
        var agg = DemandAggregates.Build(new[]
        {
            Row("L1", "M1", "V1", "2026-04", 10),
            Row("L1", "M1", "V1", "2026-05", 20),
            Row("L1", "M1", "V1", "2026-06", 30),
        });

        var v = DemandCalculator.Velocity(agg, "L1", "M1", "V1");

        Assert.Equal(20, v.Value, 6); // mean of [30,20,10]
        Assert.Equal("Location-model-variant demand", v.Basis);
        Assert.Equal("High", v.Confidence);
        Assert.Equal("Trailing 3-month average at this location.", v.Detail);
    }

    [Fact]
    public void Velocity_Tier1_HighConfidence_AtBoundaryOfExactlyTwoNonZeroMonths()
    {
        var agg = DemandAggregates.Build(new[]
        {
            Row("L1", "M1", "V1", "2026-05", 10),
            Row("L1", "M1", "V1", "2026-06", 10),
            // 2026-04 has no sales -> nz == 2
        });

        var v = DemandCalculator.Velocity(agg, "L1", "M1", "V1");

        Assert.Equal(20.0 / 3.0, v.Value, 6); // mean of [10,10,0]
        Assert.Equal("High", v.Confidence);
    }

    [Fact]
    public void Velocity_Tier1_MediumConfidence_WhenExactlyOneNonZeroMonth()
    {
        var agg = DemandAggregates.Build(new[]
        {
            Row("L1", "M1", "V1", "2026-06", 15),
        });

        var v = DemandCalculator.Velocity(agg, "L1", "M1", "V1");

        Assert.Equal(5, v.Value, 6); // mean of [15,0,0]
        Assert.Equal("Location-model-variant demand", v.Basis);
        Assert.Equal("Medium", v.Confidence);
    }

    [Fact]
    public void Velocity_Tier1_RespectsExplicitEndMonthAndWindowSize()
    {
        var agg = DemandAggregates.Build(new[]
        {
            Row("L1", "M1", "V1", "2026-01", 10),
            Row("L1", "M1", "V1", "2026-02", 20),
            Row("L1", "M1", "V1", "2026-03", 30),
            Row("L1", "M1", "V1", "2026-04", 40),
            Row("L1", "M1", "V1", "2026-05", 50),
            Row("L1", "M1", "V1", "2026-06", 60),
        });

        // Default: window [2026-06, 2026-05, 2026-04] -> mean 50.
        var def = DemandCalculator.Velocity(agg, "L1", "M1", "V1");
        Assert.Equal(50, def.Value, 6);
        Assert.Equal("Trailing 3-month average at this location.", def.Detail);

        // nMonths=2, endMonth=2026-03: window [2026-03, 2026-02] -> mean 25.
        var custom = DemandCalculator.Velocity(agg, "L1", "M1", "V1", nMonths: 2, endMonth: "2026-03");
        Assert.Equal(25, custom.Value, 6);
        Assert.Equal("High", custom.Confidence);
        Assert.Equal("Trailing 2-month average at this location.", custom.Detail);
    }

    // ----------------------------------------------------------------------
    // Velocity — Tier 2: National model-variant fallback
    // ----------------------------------------------------------------------

    [Fact]
    public void Velocity_Tier2_NationalModelVariantFallback_ScaledByHistoricalShare()
    {
        // L1 has an old (out-of-window) sale giving it historical share, but no
        // recent local sales. Other locations supply the national signal.
        var agg = DemandAggregates.Build(new[]
        {
            Row("L1", "M1", "V1", "2025-01", 10),  // historical share only
            Row("L2", "M1", "V1", "2026-04", 30),
            Row("L2", "M1", "V1", "2026-05", 30),
            Row("L2", "M1", "V1", "2026-06", 30),
        });

        var v = DemandCalculator.Velocity(agg, "L1", "M1", "V1");

        // mvTot = 10 + 90 = 100; share = 10/100 = 0.1; mean(mvVals)=30 -> 3.0
        Assert.Equal(3.0, v.Value, 6);
        Assert.Equal("National model-variant fallback", v.Basis);
        Assert.Equal("Medium", v.Confidence);
        Assert.Equal(
            "National M1 V1 demand scaled by this location's 10.0% historical share.",
            v.Detail);
    }

    // ----------------------------------------------------------------------
    // Velocity — Tier 3: Model-level fallback
    // ----------------------------------------------------------------------

    [Fact]
    public void Velocity_Tier3_ModelLevelFallback_WhenNoHistoricalShareButModelSells()
    {
        // Query location L3 never sold M1 (share == 0) so Tier 2 is skipped even
        // though national model-variant demand exists. Model-level demand falls
        // through to Tier 3.
        var agg = DemandAggregates.Build(new[]
        {
            Row("L2", "M1", "V1", "2026-04", 30),
            Row("L2", "M1", "V1", "2026-05", 30),
            Row("L2", "M1", "V1", "2026-06", 30),
            Row("L4", "M1", "V2", "2026-04", 12),
            Row("L4", "M1", "V2", "2026-05", 12),
            Row("L4", "M1", "V2", "2026-06", 12),
        });

        var v = DemandCalculator.Velocity(agg, "L3", "M1", "V1");

        // Mdl(M1,m) = 30 + 12 = 42 each month; mean 42; nLoc = {L2,L4} = 2 -> 21.
        Assert.Equal(21, v.Value, 6);
        Assert.Equal("Model-level fallback", v.Basis);
        Assert.Equal("Low", v.Confidence);
        Assert.Equal("National M1 demand divided across 2 selling locations.", v.Detail);
    }

    [Fact]
    public void Velocity_Tier3_UsesMaxOfOneWhenModelLocationCountIsOne()
    {
        var agg = DemandAggregates.Build(new[]
        {
            Row("L2", "M1", "V1", "2026-04", 30),
            Row("L2", "M1", "V1", "2026-05", 30),
            Row("L2", "M1", "V1", "2026-06", 30),
        });

        // Query variant V9 at L3: no local, share 0, but model-level demand exists.
        var v = DemandCalculator.Velocity(agg, "L3", "M1", "V9");

        Assert.Equal(30, v.Value, 6); // mean 30 / nLoc 1
        Assert.Equal("Model-level fallback", v.Basis);
        Assert.Equal("National M1 demand divided across 1 selling locations.", v.Detail);
    }

    // ----------------------------------------------------------------------
    // Velocity — Tier 4: Insufficient demand history
    // ----------------------------------------------------------------------

    [Fact]
    public void Velocity_Tier4_InsufficientHistory_WhenNothingAnywhere()
    {
        var agg = DemandAggregates.Build(new[]
        {
            Row("L2", "M2", "V2", "2026-06", 40),
        });

        var v = DemandCalculator.Velocity(agg, "L1", "M1", "V1");

        Assert.Equal(0, v.Value, 6);
        Assert.Equal("Insufficient demand history", v.Basis);
        Assert.Equal("Low", v.Confidence);
        Assert.Equal("No reliable recent demand signal for this combination.", v.Detail);
    }

    [Fact]
    public void Velocity_Tier4_WhenHistoricalShareExistsButNoRecentSignalInWindow()
    {
        // L1 has historical share (share > 0) but the whole model has no sales in
        // the trailing window, so Tier 2 (needs mvVals sum > 0) and Tier 3 fail too.
        var agg = DemandAggregates.Build(new[]
        {
            Row("L2", "M2", "V2", "2026-06", 40),  // defines LastMonth = 2026-06
            Row("L1", "M1", "V1", "2025-01", 8),   // out-of-window historical only
        });

        var v = DemandCalculator.Velocity(agg, "L1", "M1", "V1");

        Assert.Equal(0, v.Value, 6);
        Assert.Equal("Insufficient demand history", v.Basis);
    }

    // ----------------------------------------------------------------------
    // Trend — local recent vs prior
    // ----------------------------------------------------------------------

    private static DemandAggregates TrendAgg(
        string loc,
        double[] prior,   // 2026-01, 2026-02, 2026-03
        double[] recent)  // 2026-04, 2026-05, 2026-06
    {
        var rows = new List<SalesRow>
        {
            Row(loc, "M1", "V1", "2026-01", prior[0]),
            Row(loc, "M1", "V1", "2026-02", prior[1]),
            Row(loc, "M1", "V1", "2026-03", prior[2]),
            Row(loc, "M1", "V1", "2026-04", recent[0]),
            Row(loc, "M1", "V1", "2026-05", recent[1]),
            Row(loc, "M1", "V1", "2026-06", recent[2]),
        };
        return DemandAggregates.Build(rows);
    }

    [Fact]
    public void Trend_Increasing_WhenChangeAboveEightPercent()
    {
        var agg = TrendAgg("L1", new double[] { 10, 10, 10 }, new double[] { 20, 20, 20 });

        var t = DemandCalculator.Trend(agg, "L1", "M1", "V1");

        Assert.Equal(20, t.Recent, 6);
        Assert.Equal(10, t.Prior, 6);
        Assert.Equal(100, t.ChangePct, 6);
        Assert.Equal("increasing", t.Direction);
    }

    [Fact]
    public void Trend_Declining_WhenChangeBelowNegativeEightPercent()
    {
        var agg = TrendAgg("L1", new double[] { 20, 20, 20 }, new double[] { 10, 10, 10 });

        var t = DemandCalculator.Trend(agg, "L1", "M1", "V1");

        Assert.Equal(10, t.Recent, 6);
        Assert.Equal(20, t.Prior, 6);
        Assert.Equal(-50, t.ChangePct, 6);
        Assert.Equal("declining", t.Direction);
    }

    [Fact]
    public void Trend_Stable_WhenChangeWithinBand()
    {
        var agg = TrendAgg("L1", new double[] { 20, 20, 20 }, new double[] { 21, 21, 21 });

        var t = DemandCalculator.Trend(agg, "L1", "M1", "V1");

        Assert.Equal(5, t.ChangePct, 6);
        Assert.Equal("stable", t.Direction);
    }

    [Fact]
    public void Trend_Stable_AtPositiveBoundaryOfExactlyEightPercent()
    {
        var agg = TrendAgg("L1", new double[] { 100, 100, 100 }, new double[] { 108, 108, 108 });

        var t = DemandCalculator.Trend(agg, "L1", "M1", "V1");

        Assert.Equal(8, t.ChangePct, 6); // > 8 is strict, so this is "stable"
        Assert.Equal("stable", t.Direction);
    }

    [Fact]
    public void Trend_Stable_AtNegativeBoundaryOfExactlyEightPercent()
    {
        var agg = TrendAgg("L1", new double[] { 100, 100, 100 }, new double[] { 92, 92, 92 });

        var t = DemandCalculator.Trend(agg, "L1", "M1", "V1");

        Assert.Equal(-8, t.ChangePct, 6); // < -8 is strict, so this is "stable"
        Assert.Equal("stable", t.Direction);
    }

    [Fact]
    public void Trend_PriorZeroRecentPositive_YieldsHundredPercentIncreasing()
    {
        // Recent local sales but no prior local sales; recent != 0 so the national
        // fallback is NOT triggered, and the (p == 0 -> r > 0 ? 100 : 0) path runs.
        var agg = DemandAggregates.Build(new[]
        {
            Row("L1", "M1", "V1", "2026-04", 15),
            Row("L1", "M1", "V1", "2026-05", 15),
            Row("L1", "M1", "V1", "2026-06", 15),
        });

        var t = DemandCalculator.Trend(agg, "L1", "M1", "V1");

        Assert.Equal(15, t.Recent, 6);
        Assert.Equal(0, t.Prior, 6);
        Assert.Equal(100, t.ChangePct, 6);
        Assert.Equal("increasing", t.Direction);
    }

    // ----------------------------------------------------------------------
    // Trend — national fallback (local recent AND prior both zero)
    // ----------------------------------------------------------------------

    [Fact]
    public void Trend_FallsBackToNational_WhenLocalRecentAndPriorBothZero()
    {
        // Only L2 has sales; querying L1 (no local sales) drops into the national
        // fallback, which reads model-variant demand.
        var agg = DemandAggregates.Build(new[]
        {
            Row("L2", "M1", "V1", "2026-01", 10),
            Row("L2", "M1", "V1", "2026-02", 10),
            Row("L2", "M1", "V1", "2026-03", 10),
            Row("L2", "M1", "V1", "2026-04", 20),
            Row("L2", "M1", "V1", "2026-05", 20),
            Row("L2", "M1", "V1", "2026-06", 20),
        });

        var t = DemandCalculator.Trend(agg, "L1", "M1", "V1");

        Assert.Equal(20, t.Recent, 6);
        Assert.Equal(10, t.Prior, 6);
        Assert.Equal(100, t.ChangePct, 6);
        Assert.Equal("increasing", t.Direction);
    }

    [Fact]
    public void Trend_UseNationalTrue_ReadsModelVariantDirectly()
    {
        var agg = DemandAggregates.Build(new[]
        {
            Row("L2", "M1", "V1", "2026-01", 10),
            Row("L2", "M1", "V1", "2026-02", 10),
            Row("L2", "M1", "V1", "2026-03", 10),
            Row("L2", "M1", "V1", "2026-04", 20),
            Row("L2", "M1", "V1", "2026-05", 20),
            Row("L2", "M1", "V1", "2026-06", 20),
        });

        // Query a location that never sold it, but force national reads.
        var t = DemandCalculator.Trend(agg, "LX", "M1", "V1", useNational: true);

        Assert.Equal(20, t.Recent, 6);
        Assert.Equal(10, t.Prior, 6);
        Assert.Equal(100, t.ChangePct, 6);
        Assert.Equal("increasing", t.Direction);
    }

    [Fact]
    public void Trend_AllZero_AfterNationalFallback_IsStableZeroChange()
    {
        // Nothing exists for the queried combination anywhere; national fallback
        // still yields zeros -> chg = (r > 0 ? 100 : 0) = 0 -> stable.
        var agg = DemandAggregates.Build(new[]
        {
            Row("L2", "M2", "V2", "2026-06", 40), // unrelated, only sets LastMonth
        });

        var t = DemandCalculator.Trend(agg, "LZ", "MZ", "VZ");

        Assert.Equal(0, t.Recent, 6);
        Assert.Equal(0, t.Prior, 6);
        Assert.Equal(0, t.ChangePct, 6);
        Assert.Equal("stable", t.Direction);
    }
}
