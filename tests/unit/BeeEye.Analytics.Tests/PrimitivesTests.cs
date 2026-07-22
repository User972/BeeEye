using BeeEye.Analytics;
using BeeEye.Analytics.Inventory;
using Xunit;

namespace BeeEye.Analytics.Tests;

/// <summary>
/// Unit tests for the primitive helpers ported from engine.js:
/// <see cref="Statistics"/>, <see cref="MonthKey"/>, <see cref="Format"/> and
/// <see cref="Bands"/>. Values are hand-computed against the wireframe formulas.
/// </summary>
public class PrimitivesTests
{
    // ---------------------------------------------------------------------
    // Statistics.Sum
    // ---------------------------------------------------------------------

    [Fact]
    public void Sum_EmptyList_ReturnsZero()
    {
        Assert.Equal(0.0, Statistics.Sum(Array.Empty<double>()), 6);
    }

    [Fact]
    public void Sum_SingleElement_ReturnsElement()
    {
        Assert.Equal(4.25, Statistics.Sum(new[] { 4.25 }), 6);
    }

    [Fact]
    public void Sum_MultipleElements_AddsAll()
    {
        // 1.5 + 2.5 + 3 = 7
        Assert.Equal(7.0, Statistics.Sum(new[] { 1.5, 2.5, 3.0 }), 6);
    }

    [Fact]
    public void Sum_NegativeAndPositive_Cancels()
    {
        Assert.Equal(0.0, Statistics.Sum(new[] { -5.0, 2.0, 3.0 }), 6);
    }

    // ---------------------------------------------------------------------
    // Statistics.Mean
    // ---------------------------------------------------------------------

    [Fact]
    public void Mean_EmptyList_ReturnsZero()
    {
        Assert.Equal(0.0, Statistics.Mean(Array.Empty<double>()), 6);
    }

    [Fact]
    public void Mean_MultipleElements_ReturnsAverage()
    {
        // (2 + 4 + 6) / 3 = 4
        Assert.Equal(4.0, Statistics.Mean(new[] { 2.0, 4.0, 6.0 }), 6);
    }

    [Fact]
    public void Mean_SingleElement_ReturnsElement()
    {
        Assert.Equal(9.0, Statistics.Mean(new[] { 9.0 }), 6);
    }

    // ---------------------------------------------------------------------
    // Statistics.Std (population std, divides by N)
    // ---------------------------------------------------------------------

    [Fact]
    public void Std_EmptyList_ReturnsZero()
    {
        Assert.Equal(0.0, Statistics.Std(Array.Empty<double>()), 6);
    }

    [Fact]
    public void Std_SingleElement_ReturnsZero()
    {
        // Count < 2 short-circuit.
        Assert.Equal(0.0, Statistics.Std(new[] { 42.0 }), 6);
    }

    [Fact]
    public void Std_TwoElements_PopulationFormula()
    {
        // mean = 2, variance = ((1-2)^2 + (3-2)^2)/2 = 1, std = 1
        Assert.Equal(1.0, Statistics.Std(new[] { 1.0, 3.0 }), 6);
    }

    [Fact]
    public void Std_ClassicEightSample_DividesByN()
    {
        // Textbook population example: mean = 5, variance = 32/8 = 4, std = 2.
        var values = new[] { 2.0, 4.0, 4.0, 4.0, 5.0, 5.0, 7.0, 9.0 };
        Assert.Equal(2.0, Statistics.Std(values), 6);
    }

    [Fact]
    public void Std_AllEqual_ReturnsZero()
    {
        Assert.Equal(0.0, Statistics.Std(new[] { 7.0, 7.0, 7.0 }), 6);
    }

    // ---------------------------------------------------------------------
    // Statistics.Clamp
    // ---------------------------------------------------------------------

    [Fact]
    public void Clamp_WithinRange_ReturnsValue()
    {
        Assert.Equal(5.0, Statistics.Clamp(5.0, 0.0, 10.0), 6);
    }

    [Fact]
    public void Clamp_BelowLow_ReturnsLow()
    {
        Assert.Equal(0.0, Statistics.Clamp(-3.0, 0.0, 10.0), 6);
    }

    [Fact]
    public void Clamp_AboveHigh_ReturnsHigh()
    {
        Assert.Equal(10.0, Statistics.Clamp(15.0, 0.0, 10.0), 6);
    }

    [Fact]
    public void Clamp_AtLowBoundary_ReturnsBoundary()
    {
        Assert.Equal(0.0, Statistics.Clamp(0.0, 0.0, 10.0), 6);
    }

    [Fact]
    public void Clamp_AtHighBoundary_ReturnsBoundary()
    {
        Assert.Equal(10.0, Statistics.Clamp(10.0, 0.0, 10.0), 6);
    }

    // ---------------------------------------------------------------------
    // Statistics.PercentileRanker
    // ---------------------------------------------------------------------

    [Fact]
    public void PercentileRanker_EmptyPopulation_ReturnsFifty()
    {
        var rank = Statistics.PercentileRanker(Array.Empty<double>());
        Assert.Equal(50.0, rank(100.0), 6);
    }

    [Fact]
    public void PercentileRanker_SingleElement_ReturnsFifty()
    {
        // n < 2 short-circuit regardless of the query value.
        var rank = Statistics.PercentileRanker(new[] { 5.0 });
        Assert.Equal(50.0, rank(5.0), 6);
        Assert.Equal(50.0, rank(999.0), 6);
    }

    [Fact]
    public void PercentileRanker_SortsInput_AndRanksMinAtZero()
    {
        // Unsorted input proves the ranker sorts a copy internally.
        var rank = Statistics.PercentileRanker(new[] { 30.0, 10.0, 20.0 });
        Assert.Equal(0.0, rank(10.0), 6);   // nothing below min
        Assert.Equal(50.0, rank(20.0), 6);  // i=1 of (n-1)=2 -> 50
        Assert.Equal(100.0, rank(30.0), 6); // i=2 of (n-1)=2 -> 100
    }

    [Fact]
    public void PercentileRanker_TieRanksAtLowerBound()
    {
        // sorted = [10,20,20,30], n=4. For x=20 the loop stops at the FIRST 20,
        // so i=1 -> 1/(4-1)*100 = 33.3333 (lower bound, not the upper tie).
        var rank = Statistics.PercentileRanker(new[] { 10.0, 20.0, 20.0, 30.0 });
        Assert.Equal(100.0 / 3.0, rank(20.0), 6);
        Assert.Equal(0.0, rank(10.0), 6);
        Assert.Equal(100.0, rank(30.0), 6);
    }

    [Fact]
    public void PercentileRanker_ValueBelowMin_ReturnsZero()
    {
        var rank = Statistics.PercentileRanker(new[] { 10.0, 20.0, 30.0 });
        Assert.Equal(0.0, rank(-5.0), 6);
    }

    [Fact]
    public void PercentileRanker_ValueAboveMax_ExceedsHundred_NoClamp()
    {
        // Every element is strictly below x, so i reaches n=3 and the result is
        // 3/(3-1)*100 = 150. The engine deliberately does not clamp the rank.
        var rank = Statistics.PercentileRanker(new[] { 10.0, 20.0, 30.0 });
        Assert.Equal(150.0, rank(1000.0), 6);
    }

    // ---------------------------------------------------------------------
    // MonthKey.Add
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("2026-06", 3, "2026-09")]   // positive within year
    [InlineData("2026-06", -3, "2026-03")]  // negative within year
    [InlineData("2026-01", -1, "2025-12")]  // negative year-wrap
    [InlineData("2026-12", 1, "2027-01")]   // positive year-wrap
    [InlineData("2026-06", 0, "2026-06")]   // no-op
    [InlineData("2026-06", 12, "2027-06")]  // exactly one year forward
    [InlineData("2026-06", -12, "2025-06")] // exactly one year back
    [InlineData("2026-06", 24, "2028-06")]  // two years forward
    [InlineData("2026-01", -13, "2024-12")] // more than a year back
    public void Add_MovesMonthKey(string mk, int n, string expected)
    {
        Assert.Equal(expected, MonthKey.Add(mk, n));
    }

    // ---------------------------------------------------------------------
    // MonthKey.Range (inclusive)
    // ---------------------------------------------------------------------

    [Fact]
    public void Range_MultiMonth_IsInclusiveOfBothEnds()
    {
        Assert.Equal(
            new[] { "2026-01", "2026-02", "2026-03" },
            MonthKey.Range("2026-01", "2026-03"));
    }

    [Fact]
    public void Range_SameMonth_ReturnsSingle()
    {
        Assert.Equal(new[] { "2026-05" }, MonthKey.Range("2026-05", "2026-05"));
    }

    [Fact]
    public void Range_FromAfterTo_ReturnsEmpty()
    {
        Assert.Empty(MonthKey.Range("2026-05", "2026-04"));
    }

    [Fact]
    public void Range_AcrossYearBoundary_Wraps()
    {
        Assert.Equal(
            new[] { "2025-11", "2025-12", "2026-01", "2026-02" },
            MonthKey.Range("2025-11", "2026-02"));
    }

    // ---------------------------------------------------------------------
    // MonthKey.Label
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("2026-06", "Jun 26")]
    [InlineData("2026-01", "Jan 26")]
    [InlineData("2026-12", "Dec 26")]
    [InlineData("2005-03", "Mar 05")]
    public void Label_FormatsShortMonthAndTwoDigitYear(string mk, string expected)
    {
        Assert.Equal(expected, MonthKey.Label(mk));
    }

    // ---------------------------------------------------------------------
    // MonthKey.Trailing (descending, ending at endMonth)
    // ---------------------------------------------------------------------

    [Fact]
    public void Trailing_ReturnsDescendingKeysEndingAtEndMonth()
    {
        Assert.Equal(
            new[] { "2026-06", "2026-05", "2026-04" },
            MonthKey.Trailing(3, "2026-06"));
    }

    [Fact]
    public void Trailing_One_ReturnsOnlyEndMonth()
    {
        Assert.Equal(new[] { "2026-06" }, MonthKey.Trailing(1, "2026-06"));
    }

    [Fact]
    public void Trailing_AcrossYearBoundary_Wraps()
    {
        Assert.Equal(
            new[] { "2026-01", "2025-12", "2025-11" },
            MonthKey.Trailing(3, "2026-01"));
    }

    [Fact]
    public void Trailing_Zero_ReturnsEmpty()
    {
        Assert.Empty(MonthKey.Trailing(0, "2026-06"));
    }

    // ---------------------------------------------------------------------
    // MonthKey.Of
    // ---------------------------------------------------------------------

    [Fact]
    public void Of_MidMonthDate_ReturnsMonthKey()
    {
        Assert.Equal("2026-06", MonthKey.Of(new DateOnly(2026, 6, 15)));
    }

    [Fact]
    public void Of_December_PadsMonth()
    {
        Assert.Equal("2026-12", MonthKey.Of(new DateOnly(2026, 12, 1)));
    }

    [Fact]
    public void Of_January_PadsMonth()
    {
        Assert.Equal("2005-01", MonthKey.Of(new DateOnly(2005, 1, 31)));
    }

    // ---------------------------------------------------------------------
    // Format.Sar
    // ---------------------------------------------------------------------

    [Fact]
    public void Sar_Billions_UsesTwoDecimalsAndB()
    {
        Assert.Equal("SAR 1.50B", Format.Sar(1_500_000_000m));
    }

    [Fact]
    public void Sar_Millions_UsesTwoDecimalsAndM()
    {
        Assert.Equal("SAR 2.50M", Format.Sar(2_500_000m));
    }

    [Fact]
    public void Sar_Thousands_UsesOneDecimalAndK()
    {
        Assert.Equal("SAR 1.5K", Format.Sar(1500m));
    }

    [Fact]
    public void Sar_BelowThousand_RoundsToWholeNumber()
    {
        Assert.Equal("SAR 999", Format.Sar(999m));
    }

    [Fact]
    public void Sar_BelowThousand_RoundsFraction()
    {
        // Math.Round(750.4) = 750 -> "N0" => "750"
        Assert.Equal("SAR 750", Format.Sar(750.4m));
    }

    [Fact]
    public void Sar_Zero_FormatsAsZero()
    {
        Assert.Equal("SAR 0", Format.Sar(0m));
    }

    [Fact]
    public void Sar_ExactlyOneThousand_UsesKBranch()
    {
        Assert.Equal("SAR 1.0K", Format.Sar(1000m));
    }

    [Fact]
    public void Sar_ExactlyOneMillion_UsesMBranch()
    {
        Assert.Equal("SAR 1.00M", Format.Sar(1_000_000m));
    }

    [Fact]
    public void Sar_ExactlyOneBillion_UsesBBranch()
    {
        Assert.Equal("SAR 1.00B", Format.Sar(1_000_000_000m));
    }

    [Fact]
    public void Sar_NegativeMillions_KeepsSign()
    {
        // Abs picks the branch; the signed value is formatted.
        Assert.Equal("SAR -2.50M", Format.Sar(-2_500_000m));
    }

    [Fact]
    public void Sar_NegativeBelowThousand_KeepsSign()
    {
        Assert.Equal("SAR -750", Format.Sar(-750m));
    }

    // ---------------------------------------------------------------------
    // Format.SignPct
    // ---------------------------------------------------------------------

    [Fact]
    public void SignPct_Positive_AddsPlusSign()
    {
        Assert.Equal("+5.5%", Format.SignPct(5.5));
    }

    [Fact]
    public void SignPct_Negative_UsesMinusFromNumber()
    {
        Assert.Equal("-3.5%", Format.SignPct(-3.5));
    }

    [Fact]
    public void SignPct_Zero_TreatedAsPositive()
    {
        Assert.Equal("+0.0%", Format.SignPct(0.0));
    }

    [Fact]
    public void SignPct_CustomDecimals_Positive()
    {
        Assert.Equal("+3.25%", Format.SignPct(3.25, 2));
    }

    [Fact]
    public void SignPct_CustomDecimals_Negative()
    {
        Assert.Equal("-1.25%", Format.SignPct(-1.25, 2));
    }

    [Fact]
    public void SignPct_ZeroDecimals_NoFractionalPart()
    {
        Assert.Equal("+12%", Format.SignPct(12.0, 0));
    }

    // ---------------------------------------------------------------------
    // Bands.Aging (thresholds 30 / 60 / 90 / 120)
    // ---------------------------------------------------------------------

    private static readonly int[] AgingThresholds = { 30, 60, 90, 120 };

    [Theory]
    [InlineData(0, "New")]
    [InlineData(30, "New")]              // upper boundary of New
    [InlineData(31, "Healthy")]          // just past New
    [InlineData(60, "Healthy")]          // upper boundary of Healthy
    [InlineData(61, "Watch")]            // just past Healthy
    [InlineData(90, "Watch")]            // upper boundary of Watch
    [InlineData(91, "High attention")]   // just past Watch
    [InlineData(120, "High attention")]  // upper boundary of High attention
    [InlineData(121, "Critical aging")]  // past High attention
    [InlineData(500, "Critical aging")]
    public void Aging_BandsAtBoundaries(int ageDays, string expected)
    {
        Assert.Equal(expected, Bands.Aging(ageDays, AgingThresholds));
    }

    // ---------------------------------------------------------------------
    // Bands.Manufacturing (0-180 / 181-270 / 271-365 / 365+)
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(0, "0–180 days")]
    [InlineData(180, "0–180 days")]    // upper boundary
    [InlineData(181, "181–270 days")]  // just past
    [InlineData(270, "181–270 days")]  // upper boundary
    [InlineData(271, "271–365 days")]  // just past
    [InlineData(365, "271–365 days")]  // upper boundary
    [InlineData(366, "365+ days")]          // past
    [InlineData(1000, "365+ days")]
    public void Manufacturing_BandsAtBoundaries(int ageDays, string expected)
    {
        Assert.Equal(expected, Bands.Manufacturing(ageDays));
    }

    // ---------------------------------------------------------------------
    // Bands.Risk (thresholds 34 / 59 / 79)
    // ---------------------------------------------------------------------

    private static readonly int[] RiskThresholds = { 34, 59, 79 };

    [Theory]
    [InlineData(0, "Low")]
    [InlineData(34, "Low")]        // upper boundary of Low
    [InlineData(35, "Medium")]     // just past Low
    [InlineData(59, "Medium")]     // upper boundary of Medium
    [InlineData(60, "High")]       // just past Medium
    [InlineData(79, "High")]       // upper boundary of High
    [InlineData(80, "Critical")]   // just past High
    [InlineData(100, "Critical")]
    public void Risk_BandsAtBoundaries(int score, string expected)
    {
        Assert.Equal(expected, Bands.Risk(score, RiskThresholds));
    }
}
