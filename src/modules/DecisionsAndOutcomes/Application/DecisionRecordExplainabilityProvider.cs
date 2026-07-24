using BeeEye.Analytics.Explainability;
using BeeEye.Modules.DecisionsAndOutcomes.Contracts;

namespace BeeEye.Modules.DecisionsAndOutcomes.Application;

/// <summary>
/// Explains one <b>frozen</b> recommendation from the governed Decision Log (V3-DS-006).
/// <para>
/// Distinct from the cockpit's <c>decision</c> kind on purpose. The cockpit explains a <i>live</i>
/// ranked decision, matched by rule id against a feed that surfaces one current decision per rule — the
/// right answer for a screen showing that live feed. The Decision Log is different: it is append-only
/// history, and it holds many records under one rule id (e.g. many <c>D-INV-1</c>s, one per generation
/// run, each frozen about a different unit). Resolving a log record through the live feed by rule id
/// therefore explains the wrong subject, or 404s when nothing is currently exceptional. This provider
/// resolves the record the drawer was opened over by its <b>unique id</b>, so it always explains the
/// exact frozen original the reader is looking at.
/// </para>
/// <para>
/// <b>Ownership is deliberately null.</b> The workflow footer routes a figure <i>into</i> the Decision
/// Log; here the reader is already in it, looking at the record itself with its own action controls.
/// A second, fuzzy-searched footer would be redundant and could offer to act on a different record that
/// shares the subject. The Ownership <i>section</i> would only restate the owner the detail panel
/// already shows.
/// </para>
/// <para>
/// <b>Nothing here is authored by a model.</b> Every field restates the engine output exactly as it was
/// frozen at generation (ADR 0006 §2.6). The human decision is recorded separately and never edits it.
/// </para>
/// </summary>
public sealed class DecisionRecordExplainabilityProvider(DecisionService decisions) : IExplainabilityProvider
{
    /// <summary>A frozen Decision Log recommendation, referenced by its unique id (a GUID).</summary>
    public const string DecisionRecordKind = "decision-record";

    public IReadOnlySet<string> SubjectKinds { get; } =
        new HashSet<string>(StringComparer.Ordinal) { DecisionRecordKind };

    public async Task<Explanation?> ExplainAsync(
        string subjectKind, string subjectRef, CancellationToken cancellationToken)
    {
        if (!SubjectKinds.Contains(subjectKind))
        {
            return null;
        }

        // A record's identity is a GUID. A reference that is not one is a caller error, not a data gap —
        // returning null makes it a 404, the same as a well-formed id that matches nothing.
        if (!Guid.TryParse(subjectRef, out var recommendationId))
        {
            return null;
        }

        var snapshot = await decisions.GetSnapshotAsync(recommendationId, cancellationToken);
        return snapshot is null ? null : Map(snapshot);
    }

    private static Explanation Map(RecommendationSnapshotDto r)
    {
        var lowConfidence = string.Equals(r.Confidence, "Low", StringComparison.Ordinal);

        return new Explanation(
            // Named the way the record names its subject — the frozen subject reference, not a
            // reconstructed one. The rule id and status live in the sections below.
            Title: r.SubjectRef,
            Module: $"Decision Log · {r.Area}",

            Label: lowConfidence ? OutputLabel.LowConfidence : OutputLabel.Recommendation,

            Recommendation: string.IsNullOrWhiteSpace(r.Rationale)
                ? r.Action
                : $"{r.Action}. {r.Rationale}",

            Impacts:
            [
                new("Expected impact", ExplanationFormat.Sar(r.ImpactSar), ImpactTone.Neutral),
                new("Priority", $"{r.Priority}/100", ImpactTone.Neutral),
            ],

            Confidence: new ConfidenceStatement(
                Band(r.Confidence),

                // No percentage: the confidence was frozen as a word, not a calibrated probability.
                // Inventing a number to fill the slot is the false precision this panel exists to remove.
                Percent: null,
                Why:
                [
                    $"Recorded at generation as {r.Confidence} confidence.",
                    "This is the recommendation exactly as the engine wrote it. The human decision is "
                        + "recorded separately in the Decision Log and never edits the original.",
                ]),

            // The engine's own evidence lines, verbatim. Empty for a record that carried none, which
            // omits the section rather than inventing drivers after the fact.
            Drivers: [.. r.Evidence.Select(e => new Driver(e, null))],

            // The frozen record carries no chartable series; the section is omitted rather than drawn
            // from nothing.
            Evidence: null,

            Assumptions:
            [
                .. r.Assumptions,
                $"Frozen at generation on {ExplanationFormat.Date(r.AnalysisDate)} under ruleset "
                    + $"{r.RulesetVersion}. It is not recomputed when you open it — a record always "
                    + "reproduces the figures it was generated with.",
                "Advisory only. BeeEye records what a human decides; it never writes to Oracle Fusion.",
            ],

            Lineage: Lineage(r),

            Model: new ModelInfo(
                Name: $"Decision rule {r.RuleId}",
                Version: $"ruleset {r.RulesetVersion} · dataset {r.DatasetVersion}",
                Recalculated: $"Frozen at generation — {ExplanationFormat.Date(r.AnalysisDate)}",
                Horizon: r.ValidUntilUtc is { } validUntil
                    ? $"Review window to {ExplanationFormat.Timestamp(validUntil)}"
                    : "No review window recorded",
                Validation: "Deterministic rule set — reproducible from the same inputs",
                Error: "rule-based"),

            // See the type summary: the reader is already in the Decision Log, so no workflow footer.
            Ownership: null,
            IsDemoData: r.IsDemoData);
    }

    private static IReadOnlyList<LineageNode> Lineage(RecommendationSnapshotDto r)
    {
        var nodes = new List<LineageNode>();

        if (r.IsDemoData)
        {
            nodes.Add(new LineageNode("Synthetic demo fixture", LineageKind.Demo));
        }

        nodes.Add(new LineageNode(
            $"Decision engine — {r.RuleId} (ruleset {r.RulesetVersion})", LineageKind.Derived));

        return nodes;
    }

    private static ConfidenceBand Band(string confidence) => confidence switch
    {
        "High" => ConfidenceBand.High,
        "Low" => ConfidenceBand.Low,
        _ => ConfidenceBand.Medium,
    };
}
