using BeeEye.Analytics.Explainability;
using BeeEye.Modules.Predictions.Application;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BeeEye.UnitTests.Explainability;

/// <summary>
/// Tests for <see cref="ExplainabilityService"/> — the S3 aggregation seam.
/// <para>
/// Like <c>DecisionFeedService</c>, it holds no domain rules and no data access, so it is exercised
/// with in-memory <see cref="IExplainabilityProvider"/> fakes.
/// </para>
/// </summary>
public sealed class ExplainabilityServiceTests
{
    private static ExplainabilityService Service(params IExplainabilityProvider[] providers) =>
        new(providers, NullLogger<ExplainabilityService>.Instance);

    private static Explanation Explanation(string title = "ES 350 ZX") => new(
        Title: title,
        Module: "Inventory Intelligence",
        Label: OutputLabel.Recommendation,
        Recommendation: "Transfer stock.",
        Impacts: [],
        Confidence: null,
        Drivers: [],
        Evidence: null,
        Assumptions: [],
        Lineage: [new LineageNode("Inventory workbook", LineageKind.Workbook)],
        Model: null,
        Ownership: null);

    private sealed class FakeProvider(string kind, Explanation? explanation, string? matchesRef = null)
        : IExplainabilityProvider
    {
        public IReadOnlySet<string> SubjectKinds { get; } = new HashSet<string>(StringComparer.Ordinal) { kind };

        public int Calls { get; private set; }

        public Task<Explanation?> ExplainAsync(string subjectKind, string subjectRef, CancellationToken ct)
        {
            Calls++;

            var matches = matchesRef is null || string.Equals(matchesRef, subjectRef, StringComparison.Ordinal);
            return Task.FromResult(matches ? explanation : null);
        }
    }

    private sealed class MultiKindProvider(params string[] kinds) : IExplainabilityProvider
    {
        public IReadOnlySet<string> SubjectKinds { get; } = new HashSet<string>(kinds, StringComparer.Ordinal);

        public Task<Explanation?> ExplainAsync(string subjectKind, string subjectRef, CancellationToken ct) =>
            Task.FromResult<Explanation?>(Explanation($"{subjectKind}:{subjectRef}"));
    }

    private sealed class ThrowingProvider(string kind, Exception? failure = null) : IExplainabilityProvider
    {
        public IReadOnlySet<string> SubjectKinds { get; } = new HashSet<string>(StringComparer.Ordinal) { kind };

        public Task<Explanation?> ExplainAsync(string subjectKind, string subjectRef, CancellationToken ct) =>
            throw failure ?? new InvalidOperationException("Secret connection string: Host=db;Password=hunter2");
    }

    // ---------------------------------------------------------------- registration

    [Fact]
    public void Duplicate_subject_kinds_across_providers_throw_at_composition()
    {
        // A wiring bug, and the request path is the wrong place to find it: whichever provider
        // answered would depend on dependency-injection registration order.
        var error = Assert.Throws<InvalidOperationException>(() =>
            Service(new FakeProvider("inventory-unit", Explanation()), new FakeProvider("inventory-unit", Explanation())));

        Assert.Contains("inventory-unit", error.Message, StringComparison.Ordinal);
        Assert.Contains("Exactly one provider", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_provider_claiming_several_kinds_registers_all_of_them()
    {
        var service = Service(new MultiKindProvider("decision", "brief"));

        Assert.Equal(["brief", "decision"], service.RegisteredKinds);
    }

    [Fact]
    public void Registered_kinds_are_ordered_so_the_error_message_is_stable()
    {
        var service = Service(
            new FakeProvider("part", Explanation()),
            new FakeProvider("forecast-scope", Explanation()),
            new FakeProvider("inventory-unit", Explanation()));

        Assert.Equal(["forecast-scope", "inventory-unit", "part"], service.RegisteredKinds);
    }

    [Fact]
    public void No_providers_registers_no_kinds_rather_than_failing()
    {
        Assert.Empty(Service().RegisteredKinds);
    }

    // ---------------------------------------------------------------- aggregation

    [Fact]
    public async Task The_provider_claiming_the_kind_answers()
    {
        var outcome = await Service(
                new FakeProvider("inventory-unit", Explanation("ES 350 ZX")),
                new FakeProvider("part", Explanation("Brake pad")))
            .ExplainAsync("inventory-unit", "STK-1", CancellationToken.None);

        Assert.Equal(ExplanationStatus.Explained, outcome.Status);
        Assert.Equal("ES 350 ZX", outcome.Explanation?.Title);
        Assert.Empty(outcome.Gaps);
    }

    [Fact]
    public async Task A_provider_that_does_not_claim_the_kind_is_never_called()
    {
        var other = new FakeProvider("part", Explanation());

        await Service(new FakeProvider("inventory-unit", Explanation()), other)
            .ExplainAsync("inventory-unit", "STK-1", CancellationToken.None);

        Assert.Equal(0, other.Calls);
    }

    [Fact]
    public async Task An_unclaimed_kind_is_reported_as_unknown_rather_than_not_found()
    {
        // Different answers to different questions: "that is not a thing we explain" is a caller
        // error (400), while "no such unit" is a missing subject (404).
        var outcome = await Service(new FakeProvider("inventory-unit", Explanation()))
            .ExplainAsync("space-station", "MIR", CancellationToken.None);

        Assert.Equal(ExplanationStatus.UnknownKind, outcome.Status);
        Assert.Null(outcome.Explanation);
        Assert.Empty(outcome.Gaps);
    }

    [Fact]
    public async Task A_claimed_kind_with_no_matching_reference_is_not_found()
    {
        var outcome = await Service(new FakeProvider("inventory-unit", Explanation(), matchesRef: "STK-1"))
            .ExplainAsync("inventory-unit", "STK-NOPE", CancellationToken.None);

        Assert.Equal(ExplanationStatus.NotFound, outcome.Status);
        Assert.Null(outcome.Explanation);
        Assert.Empty(outcome.Gaps);
    }

    // ---------------------------------------------------------------- resilience

    [Fact]
    public async Task A_throwing_provider_becomes_a_gap_rather_than_an_exception()
    {
        var outcome = await Service(new ThrowingProvider("inventory-unit"))
            .ExplainAsync("inventory-unit", "STK-1", CancellationToken.None);

        Assert.Equal(ExplanationStatus.Failed, outcome.Status);
        Assert.Null(outcome.Explanation);

        var gap = Assert.Single(outcome.Gaps);
        Assert.Equal("inventory-unit", gap.Area);
        Assert.Contains("could not be assembled", gap.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_gap_never_leaks_exception_detail()
    {
        var outcome = await Service(new ThrowingProvider("inventory-unit"))
            .ExplainAsync("inventory-unit", "STK-1", CancellationToken.None);

        var gap = Assert.Single(outcome.Gaps);
        Assert.DoesNotContain("Password", gap.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection string", gap.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvalidOperationException", gap.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failure_is_distinguished_from_a_missing_subject()
    {
        // The distinction the whole contract turns on: "we could not reach the data" must never be
        // rendered as "this figure carries no explanation".
        var failed = await Service(new ThrowingProvider("inventory-unit"))
            .ExplainAsync("inventory-unit", "STK-1", CancellationToken.None);

        var missing = await Service(new FakeProvider("inventory-unit", explanation: null))
            .ExplainAsync("inventory-unit", "STK-1", CancellationToken.None);

        Assert.Equal(ExplanationStatus.Failed, failed.Status);
        Assert.Equal(ExplanationStatus.NotFound, missing.Status);
    }

    [Fact]
    public async Task Cancellation_propagates_and_is_not_misreported_as_a_gap()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var service = Service(new FakeProvider("inventory-unit", Explanation()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ExplainAsync("inventory-unit", "STK-1", cts.Token));
    }

    [Fact]
    public async Task A_provider_cancellation_is_not_treated_as_a_data_gap()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var service = Service(new ThrowingProvider("inventory-unit", new OperationCanceledException()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ExplainAsync("inventory-unit", "STK-1", cts.Token));
    }
}
