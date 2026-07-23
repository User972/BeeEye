using BeeEye.Analytics.Decisions;
using Xunit;

namespace BeeEye.Analytics.Tests;

/// <summary>
/// Tests for <see cref="DecisionFeed"/> — ranking and aggregation for the UC8 cockpit.
/// Ranking is by descending priority, then descending |impact|, then ordinal id, so the feed is
/// totally ordered and reproducible. Aggregates follow <c>decisionFeed()</c> in
/// <c>docs/wireframes-v3/engine2.js</c>: critical = severity High, lowConf = confidence &lt; 0.5,
/// dueThisWeek = dueDays &lt;= 7 (i.e. High severity only, whose window is 5 days).
/// </summary>
public sealed class DecisionFeedTests
{
    private static Decision Make(
        string id = "D-TEST-1",
        DecisionSeverity severity = DecisionSeverity.Medium,
        decimal impact = 1_000_000m,
        DecisionKind kind = DecisionKind.Risk,
        double confidence = 0.7,
        double urgency = 0.6,
        double controllability = 0.8,
        bool isDemo = false) =>
        new(
            Id: id,
            Title: $"Decision {id}",
            Area: "Inventory",
            Screen: "inventory-intelligence",
            Severity: severity,
            ImpactSar: impact,
            Kind: kind,
            Confidence01: confidence,
            WhyNow: "Because the signal crossed its threshold.",
            Action: "Review and decide.",
            Evidence: "3 locations affected",
            OwnerRole: "Inventory Manager",
            Urgency: urgency,
            Controllability: controllability,
            IsDemo: isDemo);

    // ---------------------------------------------------------------- derived members

    [Fact]
    public void Priority_IsDerivedFromTheMultiplicativeModel()
    {
        // impactF(2.5M) = 0.5; 0.5 * 0.8 * 0.9 * 1.0 = 0.36 -> 36
        var d = Make(impact: 2_500_000m, urgency: 0.8, confidence: 0.9, controllability: 1.0);
        Assert.Equal(36, d.Priority);
    }

    [Fact]
    public void DueDays_AndConfidenceBand_FollowSeverityAndConfidence()
    {
        Assert.Equal(5, Make(severity: DecisionSeverity.High).DueDays);
        Assert.Equal(20, Make(severity: DecisionSeverity.Low).DueDays);
        Assert.Equal("High", Make(confidence: 0.9).Confidence);
        Assert.Equal("Low", Make(confidence: 0.3).Confidence);
    }

    [Fact]
    public void Factors_ExposeTheFourDrivers()
    {
        var d = Make(impact: 2_500_000m, urgency: 0.8, confidence: 0.6, controllability: 0.4);
        Assert.Equal(4, d.Factors.Count);
        Assert.Equal(50, d.Factors[0].Percent);
    }

    // ---------------------------------------------------------------- Rank

    [Fact]
    public void Rank_OrdersByDescendingPriority()
    {
        var low = Make("D-LOW", impact: 100_000m, urgency: 0.2, confidence: 0.4, controllability: 0.3);
        var high = Make("D-HIGH", impact: 5_000_000m, urgency: 0.95, confidence: 0.9, controllability: 0.9);
        var mid = Make("D-MID", impact: 1_000_000m, urgency: 0.6, confidence: 0.7, controllability: 0.7);

        var ranked = DecisionFeed.Rank([low, high, mid]);

        Assert.Equal(["D-HIGH", "D-MID", "D-LOW"], ranked.Select(d => d.Id));
    }

    [Fact]
    public void Rank_BreaksPriorityTiesByLargerImpact()
    {
        // Identical weightings and identical impact factor (both saturate at the ceiling), so the
        // priorities tie and the larger absolute impact must win.
        var smaller = Make("D-B", impact: 5_000_000m);
        var larger = Make("D-A", impact: 9_000_000m);

        var ranked = DecisionFeed.Rank([smaller, larger]);

        Assert.Equal(smaller.Priority, larger.Priority);
        Assert.Equal("D-A", ranked[0].Id);
    }

    [Fact]
    public void Rank_IsTotallyOrderedSoRepeatedRunsAgree()
    {
        // Fully identical except for id: priority and impact both tie, so ordinal id decides.
        var b = Make("D-B");
        var a = Make("D-A");
        var c = Make("D-C");

        Assert.Equal(["D-A", "D-B", "D-C"], DecisionFeed.Rank([b, c, a]).Select(d => d.Id));
        Assert.Equal(["D-A", "D-B", "D-C"], DecisionFeed.Rank([c, a, b]).Select(d => d.Id));
    }

    [Fact]
    public void Rank_HandlesAnEmptyFeed()
    {
        Assert.Empty(DecisionFeed.Rank([]));
    }

    [Fact]
    public void Rank_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => DecisionFeed.Rank(null!));
    }

    [Fact]
    public void Rank_DoesNotMutateTheInput()
    {
        var input = new List<Decision> { Make("D-B", impact: 100_000m), Make("D-A", impact: 5_000_000m) };
        DecisionFeed.Rank(input);
        Assert.Equal(["D-B", "D-A"], input.Select(d => d.Id));
    }

    // ---------------------------------------------------------------- Summarise

    [Fact]
    public void Summarise_OfAnEmptyFeedIsAllZero()
    {
        var s = DecisionFeed.Summarise([]);

        Assert.Equal(0, s.Total);
        Assert.Equal(0, s.Critical);
        Assert.Equal(0, s.LowConfidence);
        Assert.Equal(0, s.DueThisWeek);
        Assert.Equal(0m, s.OpportunityValueSar);
        Assert.Equal(0m, s.RiskValueSar);
        Assert.Equal(0, s.DemoDataCount);
    }

    [Fact]
    public void Summarise_CountsCriticalAsHighSeverityOnly()
    {
        var s = DecisionFeed.Summarise([
            Make("D-1", severity: DecisionSeverity.High),
            Make("D-2", severity: DecisionSeverity.High),
            Make("D-3", severity: DecisionSeverity.Medium),
            Make("D-4", severity: DecisionSeverity.Low),
        ]);

        Assert.Equal(4, s.Total);
        Assert.Equal(2, s.Critical);
    }

    [Fact]
    public void Summarise_SeparatesOpportunityFromRiskValue()
    {
        var s = DecisionFeed.Summarise([
            Make("D-1", kind: DecisionKind.Opportunity, impact: 2_000_000m),
            Make("D-2", kind: DecisionKind.Opportunity, impact: 500_000m),
            Make("D-3", kind: DecisionKind.Risk, impact: 3_000_000m),
        ]);

        Assert.Equal(2_500_000m, s.OpportunityValueSar);
        Assert.Equal(3_000_000m, s.RiskValueSar);
    }

    [Fact]
    public void Summarise_TotalsExposureByMagnitudeSoNegativesDoNotCancel()
    {
        var s = DecisionFeed.Summarise([
            Make("D-1", kind: DecisionKind.Risk, impact: 2_000_000m),
            Make("D-2", kind: DecisionKind.Risk, impact: -2_000_000m),
        ]);

        Assert.Equal(4_000_000m, s.RiskValueSar);
    }

    [Fact]
    public void Summarise_CountsLowConfidenceBelowTheThreshold()
    {
        var s = DecisionFeed.Summarise([
            Make("D-1", confidence: 0.45),
            Make("D-2", confidence: 0.5),   // boundary is exclusive: not counted
            Make("D-3", confidence: 0.9),
        ]);

        Assert.Equal(1, s.LowConfidence);
    }

    [Fact]
    public void Summarise_CountsDueThisWeekFromTheSeverityWindow()
    {
        // Only High severity has a window (5 days) within the 7-day horizon.
        var s = DecisionFeed.Summarise([
            Make("D-1", severity: DecisionSeverity.High),
            Make("D-2", severity: DecisionSeverity.Medium),
            Make("D-3", severity: DecisionSeverity.Low),
        ]);

        Assert.Equal(1, s.DueThisWeek);
    }

    [Fact]
    public void Summarise_CountsDemoDataSoItCanBeLabelled()
    {
        var s = DecisionFeed.Summarise([
            Make("D-1", isDemo: true),
            Make("D-2", isDemo: true),
            Make("D-3"),
        ]);

        Assert.Equal(2, s.DemoDataCount);
    }

    [Fact]
    public void Summarise_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => DecisionFeed.Summarise(null!));
    }

    [Fact]
    public void RankThenSummarise_AgreeOnCount()
    {
        var candidates = new[]
        {
            Make("D-1", severity: DecisionSeverity.High, kind: DecisionKind.Opportunity),
            Make("D-2", severity: DecisionSeverity.Medium),
            Make("D-3", severity: DecisionSeverity.Low, isDemo: true),
        };

        var ranked = DecisionFeed.Rank(candidates);
        var summary = DecisionFeed.Summarise(ranked);

        Assert.Equal(ranked.Count, summary.Total);
        Assert.Equal(1, summary.Critical);
        Assert.Equal(1, summary.DemoDataCount);
    }
}
