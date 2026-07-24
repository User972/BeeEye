namespace BeeEye.Analytics.Decisions;

/// <summary>
/// One ranked entry in the Executive Decision Cockpit (UC8): a material exception raised by one of
/// the intelligence modules, carrying everything the cockpit and the explainability drawer need.
/// </summary>
/// <param name="Id">Stable rule identifier, e.g. "D-ORD-1".</param>
/// <param name="Title">One-line decision headline.</param>
/// <param name="Area">Owning business area, e.g. "Order Planning".</param>
/// <param name="Screen">Nav item id to drill into, e.g. "order-optimisation".</param>
/// <param name="Severity">Drives the due window and the cockpit's critical count.</param>
/// <param name="ImpactSar">Financial exposure or upside, in SAR.</param>
/// <param name="Kind">Whether acting avoids a loss or captures an upside.</param>
/// <param name="Confidence01">0–1 confidence in the underlying signal.</param>
/// <param name="WhyNow">Why this needs attention in this period.</param>
/// <param name="Action">The proposed action — advisory only; a human decides.</param>
/// <param name="Evidence">Supporting count or measure behind the signal.</param>
/// <param name="OwnerRole">Role expected to own the decision.</param>
/// <param name="Urgency">0–1 time pressure.</param>
/// <param name="Controllability">0–1 extent to which the business can act on it.</param>
/// <param name="IsDemo">True when the signal derives from synthetic demo data and must be labelled.</param>
public sealed record Decision(
    string Id,
    string Title,
    string Area,
    string Screen,
    DecisionSeverity Severity,
    decimal ImpactSar,
    DecisionKind Kind,
    double Confidence01,
    string WhyNow,
    string Action,
    string Evidence,
    string OwnerRole,
    double Urgency,
    double Controllability,
    bool IsDemo = false)
{
    /// <summary>0–100 multiplicative priority. Derived, never stored — see <see cref="DecisionPriority"/>.</summary>
    public int Priority => DecisionPriority.ScoreFor(ImpactSar, Urgency, Confidence01, Controllability);

    /// <summary>Review window in days, derived from <see cref="Severity"/>.</summary>
    public int DueDays => DecisionPriority.DueDays(Severity);

    /// <summary>Human-facing confidence band.</summary>
    public string Confidence => DecisionPriority.ConfidenceBand(Confidence01);

    /// <summary>The four ranked drivers behind <see cref="Priority"/>.</summary>
    public IReadOnlyList<DecisionFactor> Factors =>
        DecisionPriority.Factors(ImpactSar, Urgency, Confidence01, Controllability);
}

/// <summary>Cockpit headline aggregates over a ranked decision feed.</summary>
/// <param name="Total">Number of decisions in the feed.</param>
/// <param name="Critical">Decisions at <see cref="DecisionSeverity.High"/> — drives the nav badge.</param>
/// <param name="LowConfidence">Decisions below the low-confidence threshold, surfaced honestly.</param>
/// <param name="DueThisWeek">Decisions whose review window is 7 days or fewer.</param>
/// <param name="OpportunityValueSar">Total upside across opportunity decisions.</param>
/// <param name="RiskValueSar">Total exposure across risk decisions.</param>
/// <param name="DemoDataCount">Decisions derived from synthetic data, which must be labelled.</param>
public sealed record DecisionFeedSummary(
    int Total,
    int Critical,
    int LowConfidence,
    int DueThisWeek,
    decimal OpportunityValueSar,
    decimal RiskValueSar,
    int DemoDataCount);

/// <summary>
/// Ranking and aggregation for the Executive Decision Cockpit (UC8), ported from
/// <c>decisionFeed()</c> in <c>docs/wireframes-v3/engine2.js</c> (L515–557).
/// <para>
/// This type deliberately holds <b>no</b> module-specific rules. Each v3 decision rule is built by
/// the bounded context that owns its data and published through <c>IDecisionSignalProvider</c>; this
/// engine only ranks and aggregates whatever candidates it is handed. Keeping the engine free of
/// module knowledge preserves module isolation (CLAUDE.md rule 3) and keeps ranking exhaustively
/// unit-testable.
/// </para>
/// <para>
/// Five of the six v3 rules are live: D-ORD-1 (Recommendations), D-PRC-1 (SalesActuals), D-INV-1
/// (Inventory), D-PRT-1 (SpareParts) and D-SVC-1 (AfterSales). <b>D-SUP-1 — supplier delay
/// exposure</b> has no provider yet: the Procurement module registers no
/// <c>IDecisionSignalProvider</c>, so supplier risk never reaches the cockpit.
/// </para>
/// </summary>
public static class DecisionFeed
{
    /// <summary>
    /// Confidence <b>strictly below</b> which a decision is counted as low-confidence, matching
    /// <c>lowConf</c> in <c>engine2.js</c>. Note this is exclusive while
    /// <see cref="DecisionPriority.ConfidenceBand"/> labels exactly 0.5 as "Low", so a decision shown
    /// as "Low" at exactly 0.5 is not included in this count — a v3 quirk preserved for parity.
    /// </summary>
    public const double LowConfidenceThreshold = 0.5;

    /// <summary>Review window, in days, for the "due this week" count.</summary>
    public const int DueThisWeekDays = 7;

    /// <summary>
    /// Orders decisions by descending priority. Ties break by descending impact, then by
    /// <see cref="Decision.Id"/>, so the ordering is total and reproducible — two runs over the same
    /// data always produce the same feed.
    /// </summary>
    public static IReadOnlyList<Decision> Rank(IEnumerable<Decision> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .OrderByDescending(d => d.Priority)
            .ThenByDescending(d => Math.Abs(d.ImpactSar))
            .ThenBy(d => d.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Headline aggregates over a feed. An empty feed yields an all-zero summary.</summary>
    public static DecisionFeedSummary Summarise(IReadOnlyList<Decision> decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);

        var opportunity = 0m;
        var risk = 0m;
        var critical = 0;
        var lowConfidence = 0;
        var dueThisWeek = 0;
        var demo = 0;

        foreach (var d in decisions)
        {
            if (d.Kind == DecisionKind.Opportunity)
            {
                opportunity += Math.Abs(d.ImpactSar);
            }
            else
            {
                risk += Math.Abs(d.ImpactSar);
            }

            if (d.Severity == DecisionSeverity.High)
            {
                critical++;
            }

            if (d.Confidence01 < LowConfidenceThreshold)
            {
                lowConfidence++;
            }

            if (d.DueDays <= DueThisWeekDays)
            {
                dueThisWeek++;
            }

            if (d.IsDemo)
            {
                demo++;
            }
        }

        return new DecisionFeedSummary(
            decisions.Count, critical, lowConfidence, dueThisWeek, opportunity, risk, demo);
    }
}
