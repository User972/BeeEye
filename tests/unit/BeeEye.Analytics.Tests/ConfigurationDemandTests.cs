using System.Collections.Generic;
using System.Linq;
using BeeEye.Analytics;
using BeeEye.Analytics.Configuration;
using BeeEye.Analytics.Demand;
using Xunit;

namespace BeeEye.Analytics.Tests;

/// <summary>
/// Tests for <see cref="ConfigurationDemand"/> (UC3): Key, Analyse (rotation
/// classification, decay/trend, alerts, cold-start, stockout suspicion, region
/// shares, ordering) and Summarise (rotation counts and bands).
///
/// The shared dataset pins the max month key at 2026-06 so, with the default
/// TrailingMonths = 3, the recent window is {2026-04, 2026-05, 2026-06} and the
/// prior window is {2026-01, 2026-02, 2026-03}. Expected values are computed by
/// hand from those partitions.
/// </summary>
public class ConfigurationDemandTests
{
    private static ConfigDemandSettings Settings => ConfigDemandSettings.Default;

    private static SalesRow Row(
        string loc, string model, string variant, string month, double units, string colour, string interior)
        => new(loc, model, variant, month, units, Colour: colour, Interior: interior);

    /// <summary>
    /// Seven distinct configurations, each engineered to land on a specific
    /// rotation band / branch. TotalUnits are all distinct to make the
    /// TotalUnits-desc ordering deterministic.
    ///
    ///   A  MX/Base/Red/Black    Fast   total 15  (recentVel 3.0 == FastThreshold)
    ///   B  MX/Base/Blue/Black   Medium total 13  (recentVel 1.0 == MediumThreshold, decay -70 => alert)
    ///   F  MX/Base/Silver/Black Medium total 12  (recentVel 2.0, decay 0 => stable)
    ///   G  MX/Sport/Amber/Tan   Dead   total 8   (no recent, no prior => decay 0 stable, stockout)
    ///   E  MX/Sport/Black/Grey  Dead   total 4   (no recent, prior => decay -100, stock>0 => no stockout)
    ///   D  MX/Sport/White/Black Dead   total 3   (no recent, prior => decay -100, stock 0 => stockout)
    ///   C  MX/Sport/Green/Tan   Slow   total 2   (recentVel 0.667, no prior => decay 100 increasing)
    /// </summary>
    private static IReadOnlyList<SalesRow> BuildSales() => new List<SalesRow>
    {
        // A - Fast, increasing, two regions (London 12, Paris 3)
        Row("London", "MX", "Base", "2026-06", 3, "Red", "Black"),
        Row("London", "MX", "Base", "2026-05", 3, "Red", "Black"),
        Row("Paris",  "MX", "Base", "2026-04", 3, "Red", "Black"),
        Row("London", "MX", "Base", "2026-03", 2, "Red", "Black"),
        Row("London", "MX", "Base", "2026-02", 2, "Red", "Black"),
        Row("London", "MX", "Base", "2026-01", 2, "Red", "Black"),

        // B - Medium boundary, sharp decay => DecayAlert
        Row("London", "MX", "Base", "2026-06", 1, "Blue", "Black"),
        Row("London", "MX", "Base", "2026-05", 1, "Blue", "Black"),
        Row("London", "MX", "Base", "2026-04", 1, "Blue", "Black"),
        Row("London", "MX", "Base", "2026-03", 4, "Blue", "Black"),
        Row("London", "MX", "Base", "2026-02", 3, "Blue", "Black"),
        Row("London", "MX", "Base", "2026-01", 3, "Blue", "Black"),

        // F - Medium, flat => stable trend, decay 0
        Row("London", "MX", "Base", "2026-06", 2, "Silver", "Black"),
        Row("London", "MX", "Base", "2026-05", 2, "Silver", "Black"),
        Row("London", "MX", "Base", "2026-04", 2, "Silver", "Black"),
        Row("London", "MX", "Base", "2026-03", 2, "Silver", "Black"),
        Row("London", "MX", "Base", "2026-02", 2, "Silver", "Black"),
        Row("London", "MX", "Base", "2026-01", 2, "Silver", "Black"),

        // C - Slow (0 < recentVel < MediumThreshold), cold start, no prior
        Row("London", "MX", "Sport", "2026-06", 1, "Green", "Tan"),
        Row("London", "MX", "Sport", "2026-05", 1, "Green", "Tan"),

        // D - Dead, prior sales only, stock 0 => StockoutSuspected
        Row("London", "MX", "Sport", "2026-03", 2, "White", "Black"),
        Row("London", "MX", "Sport", "2026-02", 1, "White", "Black"),

        // E - Dead, prior sales only, stock > 0 => NOT StockoutSuspected
        Row("London", "MX", "Sport", "2026-02", 3, "Black", "Grey"),
        Row("London", "MX", "Sport", "2026-01", 1, "Black", "Grey"),

        // G - sales fully outside both windows => recent 0 & prior 0 (decay 0), stock 0 => StockoutSuspected
        Row("London", "MX", "Sport", "2025-11", 5, "Amber", "Tan"),
        Row("London", "MX", "Sport", "2025-10", 3, "Amber", "Tan"),
    };

    private static IReadOnlyDictionary<string, int> BuildStock() => new Dictionary<string, int>
    {
        [ConfigurationDemand.Key("MX", "Base", "Red", "Black")]    = 5,
        [ConfigurationDemand.Key("MX", "Base", "Blue", "Black")]   = 2,
        [ConfigurationDemand.Key("MX", "Base", "Silver", "Black")] = 10,
        [ConfigurationDemand.Key("MX", "Sport", "Black", "Grey")]  = 5,
        // C (Green), D (White), G (Amber) intentionally absent => stock 0
    };

    private static IReadOnlyList<ConfigDemandResult> Analyse()
        => ConfigurationDemand.Analyse(BuildSales(), BuildStock(), Settings);

    private static ConfigDemandResult ByColour(IReadOnlyList<ConfigDemandResult> results, string colour)
        => results.Single(r => r.Colour == colour);

    // ----------------------------------------------------------------------
    // Key
    // ----------------------------------------------------------------------

    [Fact]
    public void Key_JoinsFourPartsWithPipe()
    {
        Assert.Equal("MX|Base|Red|Black", ConfigurationDemand.Key("MX", "Base", "Red", "Black"));
    }

    [Fact]
    public void Key_HandlesEmptyParts()
    {
        Assert.Equal("M||C|", ConfigurationDemand.Key("M", "", "C", ""));
    }

    // ----------------------------------------------------------------------
    // Analyse - empty input
    // ----------------------------------------------------------------------

    [Fact]
    public void Analyse_EmptySales_ReturnsEmpty()
    {
        var result = ConfigurationDemand.Analyse(
            new List<SalesRow>(), new Dictionary<string, int>(), Settings);

        Assert.Empty(result);
    }

    // ----------------------------------------------------------------------
    // Analyse - shaping / grouping
    // ----------------------------------------------------------------------

    [Fact]
    public void Analyse_GroupsByConfig_ProducesOneResultPerCombination()
    {
        var results = Analyse();

        Assert.Equal(7, results.Count);
        // Every configuration key is distinct.
        Assert.Equal(
            7,
            results.Select(r => ConfigurationDemand.Key(r.Model, r.Variant, r.Colour, r.Interior))
                   .Distinct()
                   .Count());
    }

    [Fact]
    public void Analyse_SortsByTotalUnitsDescending()
    {
        var results = Analyse();

        var totals = results.Select(r => r.TotalUnits).ToList();
        Assert.Equal(new[] { 15.0, 13.0, 12.0, 8.0, 4.0, 3.0, 2.0 }, totals);

        // And the colours line up with that order.
        Assert.Equal(
            new[] { "Red", "Blue", "Silver", "Amber", "Black", "White", "Green" },
            results.Select(r => r.Colour).ToArray());

        for (var i = 1; i < totals.Count; i++)
        {
            Assert.True(totals[i - 1] >= totals[i]);
        }
    }

    // ----------------------------------------------------------------------
    // Analyse - Config A (Fast boundary, increasing, multi-region)
    // ----------------------------------------------------------------------

    [Fact]
    public void Analyse_ConfigA_FastBoundaryAndFields()
    {
        var a = ByColour(Analyse(), "Red");

        Assert.Equal("MX", a.Model);
        Assert.Equal("Base", a.Variant);
        Assert.Equal("Red", a.Colour);
        Assert.Equal("Black", a.Interior);

        Assert.Equal(15.0, a.TotalUnits, 6);
        Assert.Equal(6, a.MonthsWithSales);
        Assert.Equal("2026-01", a.FirstMonth);
        Assert.Equal("2026-06", a.LastMonth);

        // recent (Apr+May+Jun) = 3+3+3 = 9 => 9/3 = 3.0 == FastThreshold => Fast
        Assert.Equal(3.0, a.RecentVelocity, 6);
        // prior (Jan+Feb+Mar) = 2+2+2 = 6 => 6/3 = 2.0
        Assert.Equal(2.0, a.PriorVelocity, 6);
        // (3-2)/2*100 = 50
        Assert.Equal(50.0, a.DecayPct, 6);
        Assert.Equal("increasing", a.TrendDirection);
        Assert.Equal("Fast", a.RotationClass);

        Assert.False(a.DecayAlert);         // 50 not < -25
        Assert.False(a.IsColdStart);        // 6 months >= 3
        Assert.False(a.StockoutSuspected);  // recentVelocity != 0
        Assert.Equal(5, a.CurrentStock);
    }

    [Fact]
    public void Analyse_ConfigA_ByRegionSharesSumToOneAndTopIsLargest()
    {
        var a = ByColour(Analyse(), "Red");

        // London 3+3+2+2+2 = 12, Paris 3 ; total 15
        Assert.Equal(2, a.ByRegion.Count);

        // Ordered by units descending.
        Assert.Equal("London", a.ByRegion[0].Region);
        Assert.Equal(12.0, a.ByRegion[0].Units, 6);
        Assert.Equal(0.8, a.ByRegion[0].Share, 6);

        Assert.Equal("Paris", a.ByRegion[1].Region);
        Assert.Equal(3.0, a.ByRegion[1].Units, 6);
        Assert.Equal(0.2, a.ByRegion[1].Share, 6);

        Assert.Equal(1.0, a.ByRegion.Sum(r => r.Share), 6);
        Assert.Equal(a.ByRegion.Max(r => r.Share), a.TopRegionShare, 6);
        Assert.Equal(0.8, a.TopRegionShare, 6);
    }

    // ----------------------------------------------------------------------
    // Analyse - Config B (Medium boundary, declining, DecayAlert)
    // ----------------------------------------------------------------------

    [Fact]
    public void Analyse_ConfigB_MediumBoundaryDecliningWithAlert()
    {
        var b = ByColour(Analyse(), "Blue");

        Assert.Equal(13.0, b.TotalUnits, 6);
        Assert.Equal(6, b.MonthsWithSales);

        // recent = 1+1+1 = 3 => 1.0 == MediumThreshold (< FastThreshold) => Medium
        Assert.Equal(1.0, b.RecentVelocity, 6);
        // prior = 4+3+3 = 10 => 10/3
        Assert.Equal(10.0 / 3.0, b.PriorVelocity, 6);
        // (1 - 10/3)/(10/3)*100 = -70
        Assert.Equal(-70.0, b.DecayPct, 6);
        Assert.Equal("declining", b.TrendDirection);
        Assert.Equal("Medium", b.RotationClass);

        Assert.True(b.DecayAlert);          // -70 < -25 and priorVelocity > 0
        Assert.False(b.IsColdStart);
        Assert.False(b.StockoutSuspected);
        Assert.Equal(2, b.CurrentStock);
        Assert.Equal(1.0, b.TopRegionShare, 6); // single region
    }

    // ----------------------------------------------------------------------
    // Analyse - Config F (Medium, stable trend, decay 0)
    // ----------------------------------------------------------------------

    [Fact]
    public void Analyse_ConfigF_StableTrendNoAlert()
    {
        var f = ByColour(Analyse(), "Silver");

        Assert.Equal(12.0, f.TotalUnits, 6);
        Assert.Equal(2.0, f.RecentVelocity, 6); // 6/3
        Assert.Equal(2.0, f.PriorVelocity, 6);  // 6/3
        Assert.Equal(0.0, f.DecayPct, 6);       // (2-2)/2*100 = 0
        Assert.Equal("stable", f.TrendDirection);
        Assert.Equal("Medium", f.RotationClass);
        Assert.False(f.DecayAlert);
        Assert.False(f.IsColdStart);
        Assert.False(f.StockoutSuspected);
        Assert.Equal(10, f.CurrentStock);
    }

    // ----------------------------------------------------------------------
    // Analyse - Config C (Slow, cold start, no prior => decay 100)
    // ----------------------------------------------------------------------

    [Fact]
    public void Analyse_ConfigC_SlowColdStartIncreasingNoPrior()
    {
        var c = ByColour(Analyse(), "Green");

        Assert.Equal(2.0, c.TotalUnits, 6);
        Assert.Equal(2, c.MonthsWithSales);
        Assert.Equal("2026-05", c.FirstMonth);
        Assert.Equal("2026-06", c.LastMonth);

        // recent = 1+1 = 2 => 2/3, which is > 0 but < MediumThreshold => Slow
        Assert.Equal(2.0 / 3.0, c.RecentVelocity, 6);
        Assert.Equal(0.0, c.PriorVelocity, 6);
        // priorVelocity == 0 and recentVelocity > 0 => decay 100
        Assert.Equal(100.0, c.DecayPct, 6);
        Assert.Equal("increasing", c.TrendDirection);
        Assert.Equal("Slow", c.RotationClass);

        Assert.False(c.DecayAlert);         // priorVelocity == 0 blocks alert
        Assert.True(c.IsColdStart);         // 2 months < 3
        Assert.False(c.StockoutSuspected);  // recentVelocity != 0 even though stock 0
        Assert.Equal(0, c.CurrentStock);
    }

    // ----------------------------------------------------------------------
    // Analyse - Config D (Dead, prior only, stock 0 => StockoutSuspected)
    // ----------------------------------------------------------------------

    [Fact]
    public void Analyse_ConfigD_DeadStockoutSuspectedWhenStockZero()
    {
        var d = ByColour(Analyse(), "White");

        Assert.Equal(3.0, d.TotalUnits, 6);
        Assert.Equal(2, d.MonthsWithSales);
        Assert.Equal("2026-02", d.FirstMonth);
        Assert.Equal("2026-03", d.LastMonth);

        Assert.Equal(0.0, d.RecentVelocity, 6); // nothing in recent window
        Assert.Equal(1.0, d.PriorVelocity, 6);  // (2+1)/3
        Assert.Equal(-100.0, d.DecayPct, 6);    // (0-1)/1*100
        Assert.Equal("declining", d.TrendDirection);
        Assert.Equal("Dead", d.RotationClass);

        Assert.True(d.DecayAlert);          // -100 < -25 and priorVelocity > 0
        Assert.True(d.IsColdStart);
        Assert.True(d.StockoutSuspected);   // recentVel 0, stock 0, total > 0
        Assert.Equal(0, d.CurrentStock);
    }

    // ----------------------------------------------------------------------
    // Analyse - Config E (Dead, prior only, stock > 0 => NOT StockoutSuspected)
    // ----------------------------------------------------------------------

    [Fact]
    public void Analyse_ConfigE_DeadButStockPresentSuppressesStockout()
    {
        var e = ByColour(Analyse(), "Black");

        Assert.Equal(4.0, e.TotalUnits, 6);
        Assert.Equal(2, e.MonthsWithSales);
        Assert.Equal("2026-01", e.FirstMonth);
        Assert.Equal("2026-02", e.LastMonth);

        Assert.Equal(0.0, e.RecentVelocity, 6);
        Assert.Equal(4.0 / 3.0, e.PriorVelocity, 6); // (3+1)/3
        Assert.Equal(-100.0, e.DecayPct, 6);
        Assert.Equal("declining", e.TrendDirection);
        Assert.Equal("Dead", e.RotationClass);

        Assert.True(e.DecayAlert);
        Assert.True(e.IsColdStart);
        Assert.False(e.StockoutSuspected);  // stock 5 > 0 => suppressed
        Assert.Equal(5, e.CurrentStock);
    }

    // ----------------------------------------------------------------------
    // Analyse - Config G (out-of-window sales => recent 0 & prior 0, decay 0)
    // ----------------------------------------------------------------------

    [Fact]
    public void Analyse_ConfigG_NoRecentNoPriorZeroDecayStableDeadStockout()
    {
        var g = ByColour(Analyse(), "Amber");

        Assert.Equal(8.0, g.TotalUnits, 6);
        Assert.Equal(2, g.MonthsWithSales);
        Assert.Equal("2025-10", g.FirstMonth);
        Assert.Equal("2025-11", g.LastMonth);

        Assert.Equal(0.0, g.RecentVelocity, 6);
        Assert.Equal(0.0, g.PriorVelocity, 6);
        Assert.Equal(0.0, g.DecayPct, 6);   // both velocities 0 => decay 0
        Assert.Equal("stable", g.TrendDirection);
        Assert.Equal("Dead", g.RotationClass);

        Assert.False(g.DecayAlert);         // priorVelocity == 0
        Assert.True(g.IsColdStart);
        Assert.True(g.StockoutSuspected);   // recentVel 0, stock 0, total > 0
        Assert.Equal(0, g.CurrentStock);
    }

    // ----------------------------------------------------------------------
    // Analyse - zero-unit config exercises the totalUnits == 0 share branch
    // ----------------------------------------------------------------------

    [Fact]
    public void Analyse_ZeroTotalUnits_SharesAndTopShareAreZero()
    {
        var sales = new List<SalesRow>
        {
            Row("London", "ZQ", "Base", "2026-06", 0, "Zero", "None"),
        };

        var result = ConfigurationDemand.Analyse(sales, new Dictionary<string, int>(), Settings);

        var only = Assert.Single(result);
        Assert.Equal(0.0, only.TotalUnits, 6);
        Assert.Equal("Dead", only.RotationClass);
        Assert.Equal(0.0, only.RecentVelocity, 6);
        Assert.False(only.StockoutSuspected);   // totalUnits not > 0
        Assert.Equal(0.0, only.TopRegionShare, 6);
        var region = Assert.Single(only.ByRegion);
        Assert.Equal(0.0, region.Share, 6);     // total 0 => share defaults to 0
    }

    // ----------------------------------------------------------------------
    // Summarise
    // ----------------------------------------------------------------------

    [Fact]
    public void Summarise_CountsBandsAndFlags()
    {
        var summary = ConfigurationDemand.Summarise(Analyse());

        Assert.Equal(7, summary.Configurations);
        Assert.Equal(57.0, summary.TotalUnits, 6); // 15+13+12+8+4+3+2

        Assert.Equal(1, summary.FastCount);   // A
        Assert.Equal(2, summary.MediumCount); // B, F
        Assert.Equal(1, summary.SlowCount);   // C
        Assert.Equal(3, summary.DeadCount);   // G, E, D

        Assert.Equal(3, summary.DecayAlerts);        // B, D, E
        Assert.Equal(4, summary.ColdStart);          // C, D, E, G
        Assert.Equal(2, summary.StockoutSuspected);  // D, G
    }

    [Fact]
    public void Summarise_ByRotationHasFourBandsSummingToConfigurations()
    {
        var summary = ConfigurationDemand.Summarise(Analyse());

        Assert.Equal(4, summary.ByRotation.Count);
        Assert.Equal(
            new[] { "Fast", "Medium", "Slow", "Dead" },
            summary.ByRotation.Select(b => b.Key).ToArray());

        // Band configuration counts sum to the total configurations.
        Assert.Equal(summary.Configurations, summary.ByRotation.Sum(b => b.Configurations));

        var fast = summary.ByRotation.Single(b => b.Key == "Fast");
        var medium = summary.ByRotation.Single(b => b.Key == "Medium");
        var slow = summary.ByRotation.Single(b => b.Key == "Slow");
        var dead = summary.ByRotation.Single(b => b.Key == "Dead");

        Assert.Equal(1, fast.Configurations);
        Assert.Equal(15.0, fast.Units, 6);      // A

        Assert.Equal(2, medium.Configurations);
        Assert.Equal(25.0, medium.Units, 6);    // B 13 + F 12

        Assert.Equal(1, slow.Configurations);
        Assert.Equal(2.0, slow.Units, 6);       // C

        Assert.Equal(3, dead.Configurations);
        Assert.Equal(15.0, dead.Units, 6);      // G 8 + E 4 + D 3

        // Band units also reconcile to the summary total.
        Assert.Equal(summary.TotalUnits, summary.ByRotation.Sum(b => b.Units), 6);
    }

    [Fact]
    public void Summarise_EmptyConfigs_AllZeroWithFourBands()
    {
        var summary = ConfigurationDemand.Summarise(new List<ConfigDemandResult>());

        Assert.Equal(0, summary.Configurations);
        Assert.Equal(0.0, summary.TotalUnits, 6);
        Assert.Equal(0, summary.FastCount);
        Assert.Equal(0, summary.MediumCount);
        Assert.Equal(0, summary.SlowCount);
        Assert.Equal(0, summary.DeadCount);
        Assert.Equal(0, summary.DecayAlerts);
        Assert.Equal(0, summary.ColdStart);
        Assert.Equal(0, summary.StockoutSuspected);
        Assert.Equal(4, summary.ByRotation.Count);
        Assert.All(summary.ByRotation, b => Assert.Equal(0, b.Configurations));
    }
}
