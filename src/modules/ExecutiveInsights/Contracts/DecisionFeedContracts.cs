using BeeEye.Analytics.Decisions;

namespace BeeEye.Modules.ExecutiveInsights.Contracts;

/// <summary>One decision as returned over HTTP. A flattened projection of
/// <see cref="Decision"/>: the derived members are materialised so clients never re-implement the
/// priority model.</summary>
public sealed record DecisionDto(
    string Id,
    string Title,
    string Area,
    string Screen,
    string Severity,
    decimal ImpactSar,
    string Kind,
    int Priority,
    string Confidence,
    int ConfidencePct,
    int DueDays,
    string WhyNow,
    string Action,
    string Evidence,
    string OwnerRole,
    bool IsDemo,
    IReadOnlyList<DecisionFactorDto> Factors)
{
    public static DecisionDto From(Decision d) => new(
        d.Id,
        d.Title,
        d.Area,
        d.Screen,
        d.Severity.ToString(),
        decimal.Round(d.ImpactSar, 2, MidpointRounding.AwayFromZero),
        d.Kind.ToString(),
        d.Priority,
        d.Confidence,
        (int)Math.Round(Math.Clamp(d.Confidence01, 0, 1) * 100, MidpointRounding.AwayFromZero),
        d.DueDays,
        d.WhyNow,
        d.Action,
        d.Evidence,
        d.OwnerRole,
        d.IsDemo,
        [.. d.Factors.Select(f => new DecisionFactorDto(f.Name, f.Percent))]);
}

/// <summary>One ranked driver behind a decision's priority.</summary>
public sealed record DecisionFactorDto(string Name, int Percent);

/// <summary>Cockpit headline aggregates.</summary>
public sealed record DecisionFeedSummaryDto(
    int Total,
    int Critical,
    int LowConfidence,
    int DueThisWeek,
    decimal OpportunityValueSar,
    decimal RiskValueSar,
    int DemoDataCount)
{
    public static DecisionFeedSummaryDto From(DecisionFeedSummary s) => new(
        s.Total,
        s.Critical,
        s.LowConfidence,
        s.DueThisWeek,
        decimal.Round(s.OpportunityValueSar, 2, MidpointRounding.AwayFromZero),
        decimal.Round(s.RiskValueSar, 2, MidpointRounding.AwayFromZero),
        s.DemoDataCount);
}

/// <summary>
/// A context that could not be reached while building the feed. Reported rather than hidden: a
/// cockpit that silently drops a module would understate the decisions needing attention.
/// </summary>
/// <param name="Area">The business area whose provider failed.</param>
/// <param name="Reason">A safe, non-technical summary — never an exception message or stack trace.</param>
public sealed record DecisionFeedGapDto(string Area, string Reason);

/// <summary>The Executive Decision Cockpit payload (UC8).</summary>
/// <param name="Decisions">Ranked highest-priority first.</param>
/// <param name="Summary">Headline aggregates over <paramref name="Decisions"/>.</param>
/// <param name="Narrative">A one-line plain-language summary of where attention is needed.</param>
/// <param name="Gaps">Contexts that failed to contribute; empty when every provider succeeded.</param>
/// <param name="GeneratedAtUtc">When this feed was computed.</param>
public sealed record DecisionFeedResponse(
    IReadOnlyList<DecisionDto> Decisions,
    DecisionFeedSummaryDto Summary,
    string Narrative,
    IReadOnlyList<DecisionFeedGapDto> Gaps,
    DateTimeOffset GeneratedAtUtc);
