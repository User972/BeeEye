using BeeEye.Analytics.Decisions;
using BeeEye.Analytics.Explainability;
using BeeEye.Shared.Time;

namespace BeeEye.Modules.ExecutiveInsights.Application;

/// <summary>
/// Explains the Executive Decision Cockpit (UC8, V3-DS-006) — both a single ranked decision and the
/// monthly brief itself, which is v3's <c>ckExplainSummary</c>.
/// <para>
/// The <c>brief</c> subject is the one place in the platform where "how was this assembled?" is the
/// whole question, and it is also the one place where <b>a failure elsewhere changes the answer</b>.
/// When a contributing context fails, its gap appears in the confidence reasons — an incomplete brief
/// that does not say it is incomplete is worse than no brief, because an executive reads a short list
/// as "little needs attention" rather than "we could not look".
/// </para>
/// </summary>
public sealed class CockpitExplainabilityProvider(DecisionFeedService feed, IClock clock)
    : IExplainabilityProvider
{
    /// <summary>One ranked cockpit decision, referenced by its rule id, e.g. <c>D-INV-1</c>.</summary>
    public const string DecisionKind = "decision";

    /// <summary>The monthly brief as a whole. The reference is ignored; there is one current brief.</summary>
    public const string BriefKind = "brief";

    public IReadOnlySet<string> SubjectKinds { get; } =
        new HashSet<string>(StringComparer.Ordinal) { DecisionKind, BriefKind };

    public async Task<Explanation?> ExplainAsync(
        string subjectKind, string subjectRef, CancellationToken cancellationToken)
    {
        if (!SubjectKinds.Contains(subjectKind))
        {
            return null;
        }

        var response = await feed.BuildAsync(clock.UtcNow, cancellationToken);

        return string.Equals(subjectKind, BriefKind, StringComparison.Ordinal)
            ? Brief(response)
            : ForDecision(response, subjectRef);
    }

    // ---------------------------------------------------------------- one decision

    private static Explanation? ForDecision(Contracts.DecisionFeedResponse response, string subjectRef)
    {
        var decision = response.Decisions.FirstOrDefault(d =>
            string.Equals(d.Id, subjectRef, StringComparison.OrdinalIgnoreCase));

        if (decision is null)
        {
            return null;
        }

        return new Explanation(
            Title: decision.Title,
            Module: $"Decision Cockpit · {decision.Area}",
            Label: string.Equals(decision.Confidence, "Low", StringComparison.Ordinal)
                ? OutputLabel.LowConfidence
                : OutputLabel.Recommendation,
            Recommendation: $"{decision.Action} {decision.WhyNow}",
            Impacts:
            [
                new(decision.Kind == "Opportunity" ? "Upside at stake" : "Exposure",
                    ExplanationFormat.Sar(decision.ImpactSar),
                    decision.Kind == "Opportunity" ? ImpactTone.Positive : ImpactTone.Negative),
                new("Priority", $"{decision.Priority}/100", Tone(decision.Severity)),
                new("Severity", decision.Severity, Tone(decision.Severity)),
                new("Review window", ExplanationFormat.Days(decision.DueDays), ImpactTone.Neutral),
            ],
            Confidence: new ConfidenceStatement(
                Band(decision.Confidence),
                decision.ConfidencePct,
                Why:
                [
                    $"Raised by the {decision.Area} context, which owns the data behind it.",
                    decision.Evidence,
                    decision.IsDemo
                        ? "Derived from clearly-labelled synthetic demo data, so treat the magnitude as "
                          + "illustrative rather than measured."
                        : "Computed from the supplied ADMC workbooks.",
                ]),

            // The four factors behind the priority score, exactly as the model ranks them — not a
            // narrative about them.
            Drivers:
            [
                .. decision.Factors.Select(f => new Driver(
                    f.Name, $"{ExplanationFormat.Count(f.Percent)}% of the priority score")),
            ],

            // The cockpit charts nothing per decision; the owning screen does, and the decision links
            // there. Section omitted rather than duplicated.
            Evidence: null,
            Assumptions:
            [
                "Priority is impact × urgency × confidence × controllability, normalised to 0–100. "
                    + "A high score means \"decide this first\", not \"this is certainly right\".",
                "Impact is normalised against a SAR 5M reference, so two very large exposures compress "
                    + "toward the same impact factor.",
                "Only the highest-priority decision per exception type is surfaced. The cockpit is a "
                    + "decision queue, not a report.",
                "Advisory only. BeeEye records what a human decides; it never writes to Oracle Fusion.",
            ],
            Lineage: Lineage(decision.IsDemo),
            Model: new ModelInfo(
                Name: "Executive priority model",
                Version: "UC8 · multiplicative rule set",
                Recalculated: ExplanationFormat.Timestamp(response.GeneratedAtUtc),
                Horizon: ExplanationFormat.Days(decision.DueDays) + " review window",
                Validation: "Deterministic rule set — reproducible from the same inputs",
                Error: "rule-based"),

            // Ownership is what makes the drawer render its workflow footer, and a cockpit decision is
            // exactly the kind of subject that has one.
            Ownership: new Ownership(
                decision.OwnerRole, $"{decision.Severity} · due in {ExplanationFormat.Days(decision.DueDays)}"),
            IsDemoData: decision.IsDemo);
    }

    // ---------------------------------------------------------------- the brief itself

    private static Explanation Brief(Contracts.DecisionFeedResponse response)
    {
        var summary = response.Summary;

        return new Explanation(
            Title: "How this monthly brief was generated",
            Module: "Decision Cockpit",
            Label: OutputLabel.Calculated,
            Recommendation: response.Narrative,
            Impacts:
            [
                new("Decisions surfaced", ExplanationFormat.Count(summary.Total), ImpactTone.Neutral),
                new("Critical", ExplanationFormat.Count(summary.Critical),
                    summary.Critical > 0 ? ImpactTone.Negative : ImpactTone.Positive),
                new("Upside at stake", ExplanationFormat.Sar(summary.OpportunityValueSar), ImpactTone.Positive),
                new("Exposure", ExplanationFormat.Sar(summary.RiskValueSar), ImpactTone.Negative),
            ],
            Confidence: new ConfidenceStatement(
                // An incomplete brief is a low-confidence brief, full stop. The count of contexts that
                // failed is the single most important thing about it.
                response.Gaps.Count > 0 ? ConfidenceBand.Low : ConfidenceBand.Medium,
                Percent: null,
                Why: BriefWhy(response)),
            Drivers:
            [
                new("Executive priority score",
                    "impact × urgency × confidence × controllability, normalised 0–100"),
                new("Contributing contexts",
                    "Order Planning, Configuration, Procurement, Inventory, After-Sales, Parts"),
                new("Ranking",
                    "descending priority, ties broken by impact then rule id — so two runs over the "
                    + "same data always produce the same order"),
                new("Low-confidence decisions",
                    $"{ExplanationFormat.Count(summary.LowConfidence)} of "
                    + $"{ExplanationFormat.Count(summary.Total)} fall below the confidence threshold"),
                new("Synthetic-demo decisions",
                    $"{ExplanationFormat.Count(summary.DemoDataCount)} of "
                    + $"{ExplanationFormat.Count(summary.Total)} derive from demo fixtures"),
            ],
            Evidence: null,
            Assumptions:
            [
                "Only the highest-priority decision per exception type is surfaced by default.",
                "Every figure is a decision-support estimate requiring business review.",
                "Supplier delay exposure is absent: the platform holds no supplier, purchase-order or "
                    + "delivery-performance data, and fabricating it would put invented figures in front "
                    + "of executives as if they were measured (V3-CONFLICT-9).",
                "Procurement, after-sales and parts decisions use clearly-labelled synthetic demo data.",
            ],
            Lineage:
            [
                new LineageNode("Oracle Fusion — sales & inventory (system of record)", LineageKind.Fusion),
                new LineageNode("Sales & inventory workbooks", LineageKind.Workbook),
                new LineageNode("Synthetic demo fixtures (after-sales, parts)", LineageKind.Demo),
                new LineageNode("Cross-module priority model (UC8)", LineageKind.Derived),
            ],
            Model: new ModelInfo(
                Name: "Executive priority model",
                Version: "UC8 · multiplicative rule set",
                Recalculated: ExplanationFormat.Timestamp(response.GeneratedAtUtc),
                Horizon: "1 month",
                Validation: "Deterministic rule set — reproducible from the same inputs",
                Error: "rule-based"),
            Ownership: null,
            IsDemoData: summary.DemoDataCount > 0);
    }

    private static IReadOnlyList<string> BriefWhy(Contracts.DecisionFeedResponse response)
    {
        var why = new List<string>
        {
            $"{ExplanationFormat.Count(response.Summary.Total)} decisions were surfaced from six "
            + "contributing contexts.",
        };

        // The gaps the cockpit already reports, restated here as a confidence caveat. The contract
        // gives a provider no channel of its own for reporting another context's failure, and burying
        // it in evidence text would be exactly the kind of quiet omission this panel exists to stop.
        foreach (var gap in response.Gaps)
        {
            why.Add($"{gap.Area} could not be assessed, so this brief is incomplete.");
        }

        if (response.Gaps.Count == 0)
        {
            why.Add("Every contributing context answered, so the brief is complete for this period.");
        }

        if (response.Summary.DemoDataCount > 0)
        {
            why.Add(
                $"{ExplanationFormat.Count(response.Summary.DemoDataCount)} of the decisions derive from "
                + "synthetic demo data and are labelled individually.");
        }

        return why;
    }

    // ---------------------------------------------------------------- shared

    private static IReadOnlyList<LineageNode> Lineage(bool isDemo)
    {
        var nodes = new List<LineageNode>
        {
            new("Oracle Fusion — sales & inventory (system of record)", LineageKind.Fusion),
            new("Sales & inventory workbooks", LineageKind.Workbook),
        };

        if (isDemo)
        {
            nodes.Add(new LineageNode("Synthetic demo fixture", LineageKind.Demo));
        }

        nodes.Add(new LineageNode("Cross-module priority model (UC8)", LineageKind.Derived));
        return nodes;
    }

    private static ConfidenceBand Band(string confidence) => confidence switch
    {
        "High" => ConfidenceBand.High,
        "Low" => ConfidenceBand.Low,
        _ => ConfidenceBand.Medium,
    };

    private static ImpactTone Tone(string severity) => severity switch
    {
        "High" => ImpactTone.Negative,
        "Medium" => ImpactTone.Warning,
        _ => ImpactTone.Neutral,
    };
}
