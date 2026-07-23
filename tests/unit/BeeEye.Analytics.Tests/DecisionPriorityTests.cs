using BeeEye.Analytics.Decisions;
using Xunit;

namespace BeeEye.Analytics.Tests;

/// <summary>
/// Tests for <see cref="DecisionPriority"/> — the UC8 Executive Decision Cockpit priority model.
/// Expected values are computed by hand from the source formulas in
/// <c>docs/wireframes-v3/engine2.js</c>:
///   impactFactor = clamp(|impact| / 5_000_000, 0.15, 1)
///   priority     = round(clamp(impactF * urgency * confidence * controllability, 0, 1) * 100)
///   dueDays      = high 5 · medium 12 · low 20
///   confidence   = &gt; 0.75 High · &gt; 0.5 Medium · else Low
/// Rounding is AwayFromZero to match JavaScript's Math.round.
/// </summary>
public sealed class DecisionPriorityTests
{
    // ---------------------------------------------------------------- ImpactFactor

    [Fact]
    public void ImpactFactor_ScalesLinearlyBelowTheCeiling()
    {
        // 2.5M / 5M = 0.5
        Assert.Equal(0.5, DecisionPriority.ImpactFactor(2_500_000m), 10);
    }

    [Fact]
    public void ImpactFactor_SaturatesAtTheCeiling()
    {
        Assert.Equal(1.0, DecisionPriority.ImpactFactor(DecisionPriority.ImpactCeilingSar), 10);
    }

    [Fact]
    public void ImpactFactor_DoesNotExceedOneAboveTheCeiling()
    {
        Assert.Equal(1.0, DecisionPriority.ImpactFactor(500_000_000m), 10);
    }

    [Fact]
    public void ImpactFactor_FloorsSmallImpactsRatherThanZeroingThem()
    {
        // 1_000 / 5_000_000 = 0.0002, below the 0.15 floor.
        Assert.Equal(DecisionPriority.MinImpactFactor, DecisionPriority.ImpactFactor(1_000m), 10);
        Assert.Equal(DecisionPriority.MinImpactFactor, DecisionPriority.ImpactFactor(0m), 10);
    }

    [Fact]
    public void ImpactFactor_TreatsAnExposureAsMaterialAsAnEqualUpside()
    {
        Assert.Equal(
            DecisionPriority.ImpactFactor(2_000_000m),
            DecisionPriority.ImpactFactor(-2_000_000m),
            10);
    }

    [Theory]
    [InlineData(750_000, 0.15)]   // 0.15 exactly — the floor boundary
    [InlineData(1_000_000, 0.2)]
    [InlineData(4_000_000, 0.8)]
    public void ImpactFactor_MatchesHandComputedValues(decimal impact, double expected)
    {
        Assert.Equal(expected, DecisionPriority.ImpactFactor(impact), 10);
    }

    // ---------------------------------------------------------------- Score

    [Fact]
    public void Score_IsTheProductOfAllFourFactors()
    {
        // 1.0 * 0.8 * 0.9 * 0.5 = 0.36 -> 36
        Assert.Equal(36, DecisionPriority.Score(1.0, 0.8, 0.9, 0.5));
    }

    [Fact]
    public void Score_IsOneHundredOnlyWhenEveryFactorIsMaximal()
    {
        Assert.Equal(100, DecisionPriority.Score(1.0, 1.0, 1.0, 1.0));
    }

    [Fact]
    public void Score_IsZeroWhenAnySingleFactorIsZero()
    {
        Assert.Equal(0, DecisionPriority.Score(0.0, 1.0, 1.0, 1.0));
        Assert.Equal(0, DecisionPriority.Score(1.0, 0.0, 1.0, 1.0));
        Assert.Equal(0, DecisionPriority.Score(1.0, 1.0, 0.0, 1.0));
        Assert.Equal(0, DecisionPriority.Score(1.0, 1.0, 1.0, 0.0));
    }

    [Fact]
    public void Score_IsMultiplicativeNotAdditive()
    {
        // Three strong factors must not rescue one very weak one. A weighted mean of
        // (1, 1, 1, 0.1) would be 0.775 -> 78; the multiplicative model gives 10.
        Assert.Equal(10, DecisionPriority.Score(1.0, 1.0, 1.0, 0.1));
    }

    [Fact]
    public void Score_ClampsFactorsAboveOne()
    {
        Assert.Equal(100, DecisionPriority.Score(5.0, 5.0, 5.0, 5.0));
    }

    [Fact]
    public void Score_ClampsNegativeFactorsToZero()
    {
        Assert.Equal(0, DecisionPriority.Score(-2.0, 1.0, 1.0, 1.0));
    }

    [Fact]
    public void Score_TreatsNaNAsTheWeakestFactor()
    {
        Assert.Equal(0, DecisionPriority.Score(double.NaN, 1.0, 1.0, 1.0));
    }

    [Fact]
    public void Score_RoundsHalvesAwayFromZeroLikeJavaScript()
    {
        // 0.5 * 0.5 * 1 * 1 = 0.25 -> 25 exactly; nudge to a .5 boundary:
        // 0.605 * 1 * 1 * 1 = 0.605 -> 60.5 -> 61 (banker's rounding would give 60).
        Assert.Equal(61, DecisionPriority.Score(0.605, 1.0, 1.0, 1.0));
    }

    [Fact]
    public void ScoreFor_DerivesTheImpactFactorFromMoney()
    {
        // impactF(2.5M) = 0.5; 0.5 * 1 * 1 * 1 = 0.5 -> 50
        Assert.Equal(50, DecisionPriority.ScoreFor(2_500_000m, 1.0, 1.0, 1.0));
    }

    [Fact]
    public void ScoreFor_AgreesWithScoreGivenTheSameImpactFactor()
    {
        const decimal impact = 3_100_000m;
        Assert.Equal(
            DecisionPriority.Score(DecisionPriority.ImpactFactor(impact), 0.6, 0.7, 0.8),
            DecisionPriority.ScoreFor(impact, 0.6, 0.7, 0.8));
    }

    [Fact]
    public void Score_RanksAHighImpactCertainDecisionAboveALowImpactUncertainOne()
    {
        var strong = DecisionPriority.ScoreFor(4_500_000m, 0.85, 0.9, 0.8);
        var weak = DecisionPriority.ScoreFor(120_000m, 0.4, 0.45, 0.5);
        Assert.True(strong > weak, $"expected {strong} > {weak}");
    }

    // ---------------------------------------------------------------- DueDays

    [Theory]
    [InlineData(DecisionSeverity.High, 5)]
    [InlineData(DecisionSeverity.Medium, 12)]
    [InlineData(DecisionSeverity.Low, 20)]
    public void DueDays_MatchesTheSeverityWindow(DecisionSeverity severity, int expected)
    {
        Assert.Equal(expected, DecisionPriority.DueDays(severity));
    }

    [Fact]
    public void DueDays_ShortensAsSeverityRises()
    {
        Assert.True(DecisionPriority.DueDays(DecisionSeverity.High)
            < DecisionPriority.DueDays(DecisionSeverity.Medium));
        Assert.True(DecisionPriority.DueDays(DecisionSeverity.Medium)
            < DecisionPriority.DueDays(DecisionSeverity.Low));
    }

    // ---------------------------------------------------------------- ConfidenceBand

    [Theory]
    [InlineData(0.9, "High")]
    [InlineData(0.76, "High")]
    [InlineData(0.75, "Medium")]   // boundary is exclusive: > 0.75
    [InlineData(0.65, "Medium")]
    [InlineData(0.51, "Medium")]
    [InlineData(0.5, "Low")]       // boundary is exclusive: > 0.5
    [InlineData(0.4, "Low")]
    [InlineData(0.0, "Low")]
    public void ConfidenceBand_UsesExclusiveThresholds(double confidence, string expected)
    {
        Assert.Equal(expected, DecisionPriority.ConfidenceBand(confidence));
    }

    // ---------------------------------------------------------------- Factors

    [Fact]
    public void Factors_ReturnsTheFourDriversInDisplayOrder()
    {
        var factors = DecisionPriority.Factors(2_500_000m, 0.8, 0.6, 0.4);

        Assert.Collection(
            factors,
            f => Assert.Equal("Business impact", f.Name),
            f => Assert.Equal("Urgency", f.Name),
            f => Assert.Equal("Confidence", f.Name),
            f => Assert.Equal("Controllability", f.Name));
    }

    [Fact]
    public void Factors_ExpressesEachDriverAsAWholePercentage()
    {
        var factors = DecisionPriority.Factors(2_500_000m, 0.8, 0.6, 0.4);

        Assert.Equal(50, factors[0].Percent);  // impactF 0.5
        Assert.Equal(80, factors[1].Percent);
        Assert.Equal(60, factors[2].Percent);
        Assert.Equal(40, factors[3].Percent);
    }

    [Fact]
    public void Factors_ClampsOutOfRangeWeightings()
    {
        var factors = DecisionPriority.Factors(1_000m, 2.0, -1.0, double.NaN);

        Assert.Equal(15, factors[0].Percent);  // impact floor
        Assert.Equal(100, factors[1].Percent);
        Assert.Equal(0, factors[2].Percent);
        Assert.Equal(0, factors[3].Percent);
    }
}
