using BeeEye.Analytics.Decisions;
using BeeEye.Modules.ExecutiveInsights.Application;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BeeEye.UnitTests.ExecutiveInsights;

/// <summary>
/// Tests for <see cref="DecisionFeedService"/> — the UC8 cockpit aggregation seam.
/// The service holds no domain rules and no data access, so it is exercised with in-memory
/// <see cref="IDecisionSignalProvider"/> fakes.
/// </summary>
public sealed class DecisionFeedServiceTests
{
    private static readonly DateTimeOffset At = new(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);

    private static DecisionFeedService Service(params IDecisionSignalProvider[] providers) =>
        new(providers, NullLogger<DecisionFeedService>.Instance);

    private static Decision Decision(
        string id,
        string area = "Inventory",
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
            Area: area,
            Screen: "inventory-intelligence",
            Severity: severity,
            ImpactSar: impact,
            Kind: kind,
            Confidence01: confidence,
            WhyNow: "Threshold crossed.",
            Action: "Review and decide.",
            Evidence: "3 units affected",
            OwnerRole: "Inventory Manager",
            Urgency: urgency,
            Controllability: controllability,
            IsDemo: isDemo);

    private sealed class FakeProvider(string area, params Decision[] decisions) : IDecisionSignalProvider
    {
        public string Area { get; } = area;

        public int Calls { get; private set; }

        public Task<IReadOnlyList<Decision>> GetDecisionsAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<Decision>>(decisions);
        }
    }

    private sealed class ThrowingProvider(string area, Exception? failure = null) : IDecisionSignalProvider
    {
        public string Area { get; } = area;

        public Task<IReadOnlyList<Decision>> GetDecisionsAsync(CancellationToken cancellationToken) =>
            throw failure ?? new InvalidOperationException("Secret connection string: Host=db;Password=hunter2");
    }

    // ---------------------------------------------------------------- aggregation

    [Fact]
    public async Task BuildAsync_WithNoProviders_ReturnsAnEmptyFeed()
    {
        var feed = await Service().BuildAsync(At, CancellationToken.None);

        Assert.Empty(feed.Decisions);
        Assert.Equal(0, feed.Summary.Total);
        Assert.Empty(feed.Gaps);
        Assert.Equal(At, feed.GeneratedAtUtc);
    }

    [Fact]
    public async Task BuildAsync_CollectsFromEveryProvider()
    {
        var feed = await Service(
                new FakeProvider("Inventory", Decision("D-INV-1")),
                new FakeProvider("Parts", Decision("D-PRT-1", area: "Parts")))
            .BuildAsync(At, CancellationToken.None);

        Assert.Equal(2, feed.Decisions.Count);
        Assert.Equal(2, feed.Summary.Total);
    }

    [Fact]
    public async Task BuildAsync_CallsEachProviderExactlyOnce()
    {
        var inventory = new FakeProvider("Inventory", Decision("D-INV-1"));
        var parts = new FakeProvider("Parts", Decision("D-PRT-1", area: "Parts"));

        await Service(inventory, parts).BuildAsync(At, CancellationToken.None);

        Assert.Equal(1, inventory.Calls);
        Assert.Equal(1, parts.Calls);
    }

    [Fact]
    public async Task BuildAsync_RanksByPriorityRegardlessOfProviderOrder()
    {
        var weak = Decision("D-LOW", impact: 100_000m, urgency: 0.2, confidence: 0.4, controllability: 0.3);
        var strong = Decision("D-HIGH", impact: 5_000_000m, urgency: 0.95, confidence: 0.9, controllability: 0.9);

        var feed = await Service(new FakeProvider("A", weak), new FakeProvider("B", strong))
            .BuildAsync(At, CancellationToken.None);

        Assert.Equal("D-HIGH", feed.Decisions[0].Id);
        Assert.Equal("D-LOW", feed.Decisions[1].Id);
    }

    [Fact]
    public async Task BuildAsync_SummarisesAcrossProviders()
    {
        var feed = await Service(
                new FakeProvider("A", Decision("D-1", severity: DecisionSeverity.High, kind: DecisionKind.Opportunity, impact: 2_000_000m)),
                new FakeProvider("B", Decision("D-2", impact: 3_000_000m, confidence: 0.3, isDemo: true)))
            .BuildAsync(At, CancellationToken.None);

        Assert.Equal(2, feed.Summary.Total);
        Assert.Equal(1, feed.Summary.Critical);
        Assert.Equal(1, feed.Summary.LowConfidence);
        Assert.Equal(1, feed.Summary.DueThisWeek);
        Assert.Equal(1, feed.Summary.DemoDataCount);
        Assert.Equal(2_000_000m, feed.Summary.OpportunityValueSar);
        Assert.Equal(3_000_000m, feed.Summary.RiskValueSar);
    }

    // ---------------------------------------------------------------- resilience

    [Fact]
    public async Task BuildAsync_WhenOneProviderFails_StillReturnsTheOthers()
    {
        var feed = await Service(
                new ThrowingProvider("Parts"),
                new FakeProvider("Inventory", Decision("D-INV-1")))
            .BuildAsync(At, CancellationToken.None);

        Assert.Single(feed.Decisions);
        Assert.Equal("D-INV-1", feed.Decisions[0].Id);
    }

    [Fact]
    public async Task BuildAsync_ReportsAFailedProviderAsAGapRatherThanHidingIt()
    {
        var feed = await Service(
                new ThrowingProvider("Parts"),
                new FakeProvider("Inventory", Decision("D-INV-1")))
            .BuildAsync(At, CancellationToken.None);

        var gap = Assert.Single(feed.Gaps);
        Assert.Equal("Parts", gap.Area);
        Assert.Contains("could not be assessed", gap.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildAsync_NeverLeaksExceptionDetailIntoTheGapReason()
    {
        var feed = await Service(new ThrowingProvider("Parts")).BuildAsync(At, CancellationToken.None);

        var gap = Assert.Single(feed.Gaps);
        Assert.DoesNotContain("Password", gap.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection string", gap.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvalidOperationException", gap.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_WhenEveryProviderFails_ReturnsAnEmptyFeedWithEveryGap()
    {
        var feed = await Service(new ThrowingProvider("Parts"), new ThrowingProvider("Inventory"))
            .BuildAsync(At, CancellationToken.None);

        Assert.Empty(feed.Decisions);
        Assert.Equal(2, feed.Gaps.Count);
        Assert.Equal(0, feed.Summary.Total);
    }

    [Fact]
    public async Task BuildAsync_PropagatesCancellationRatherThanReportingAFalseGap()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var service = Service(new FakeProvider("Inventory", Decision("D-INV-1")));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.BuildAsync(At, cts.Token));
    }

    [Fact]
    public async Task BuildAsync_DoesNotTreatAProviderCancellationAsADataGap()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var service = Service(new ThrowingProvider("Parts", new OperationCanceledException()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.BuildAsync(At, cts.Token));
    }

    // ---------------------------------------------------------------- projection

    [Fact]
    public async Task BuildAsync_MaterialisesDerivedMembersSoClientsNeedNoPriorityModel()
    {
        var feed = await Service(new FakeProvider(
                "Inventory",
                Decision("D-1", impact: 2_500_000m, urgency: 0.8, confidence: 0.9, controllability: 1.0)))
            .BuildAsync(At, CancellationToken.None);

        var dto = feed.Decisions[0];
        Assert.Equal(36, dto.Priority);          // 0.5 * 0.8 * 0.9 * 1.0 = 0.36
        Assert.Equal("High", dto.Confidence);
        Assert.Equal(90, dto.ConfidencePct);
        Assert.Equal(12, dto.DueDays);           // Medium severity
        Assert.Equal("Medium", dto.Severity);
        Assert.Equal("Risk", dto.Kind);
        Assert.Equal(4, dto.Factors.Count);
    }

    [Fact]
    public async Task BuildAsync_RoundsMoneyToTwoDecimalPlaces()
    {
        var feed = await Service(new FakeProvider("Inventory", Decision("D-1", impact: 1_234.5678m)))
            .BuildAsync(At, CancellationToken.None);

        Assert.Equal(1_234.57m, feed.Decisions[0].ImpactSar);
    }

    // ---------------------------------------------------------------- narrative

    // Exercised through BuildAsync rather than the internal helper, so the tests cover the path the
    // endpoint actually takes.

    private static async Task<string> NarrativeFor(params Decision[] decisions)
    {
        var feed = await Service(new FakeProvider("Mixed", decisions)).BuildAsync(At, CancellationToken.None);
        return feed.Narrative;
    }

    [Fact]
    public async Task Narrative_WithNoDecisions_SaysSoPlainly()
    {
        var feed = await Service().BuildAsync(At, CancellationToken.None);

        Assert.Equal("No material exceptions need a decision this period.", feed.Narrative);
    }

    [Fact]
    public async Task Narrative_UsesSingularForOneDecision()
    {
        var text = await NarrativeFor(Decision("D-1", area: "Inventory"));

        Assert.StartsWith("1 decision needs attention:", text, StringComparison.Ordinal);
        Assert.EndsWith(".", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Narrative_GroupsSupplyAndAfterSalesAreas()
    {
        var text = await NarrativeFor(
            Decision("D-1", area: "Inventory"),
            Decision("D-2", area: "Order Planning"),
            Decision("D-3", area: "Procurement"),
            Decision("D-4", area: "Parts"));

        Assert.Contains("4 decisions need attention", text, StringComparison.Ordinal);
        Assert.Contains("3 relate to inventory, ordering and procurement exposure", text, StringComparison.Ordinal);
        Assert.Contains("1 to after-sales and parts availability", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Narrative_CountsUnrecognisedAreasAsSalesAndConfiguration()
    {
        var text = await NarrativeFor(Decision("D-1", area: "Sales"));

        Assert.Contains("1 to sales and configuration mix", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Narrative_JoinsExactlyTwoClausesWithAnd()
    {
        var text = await NarrativeFor(
            Decision("D-1", area: "Inventory"),
            Decision("D-2", area: "Parts"));

        Assert.Contains(" and ", text, StringComparison.Ordinal);
        Assert.DoesNotContain(", and", text, StringComparison.Ordinal);
    }
}
