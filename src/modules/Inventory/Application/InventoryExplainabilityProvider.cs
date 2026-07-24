using BeeEye.Analytics.Explainability;
using BeeEye.Analytics.Inventory;

namespace BeeEye.Modules.Inventory.Application;

/// <summary>
/// Explains one inventory unit's overstock risk and the action UC5 recommends for it (V3-DS-006).
/// <para>
/// This is the <b>richest</b> of the eight providers, and deliberately the first written: the UC5
/// engine already produces an additive factor breakdown, so the drivers and the evidence series are
/// the model's own arithmetic rather than a narrative assembled after the fact. Nothing here computes
/// anything — every number is read straight off <see cref="InventoryUnitRisk"/>.
/// </para>
/// </summary>
public sealed class InventoryExplainabilityProvider(InventoryReadService inventory) : IExplainabilityProvider
{
    /// <summary>The subject is a single unit, referenced by its stock id.</summary>
    public const string InventoryUnitKind = "inventory-unit";

    public IReadOnlySet<string> SubjectKinds { get; } =
        new HashSet<string>(StringComparer.Ordinal) { InventoryUnitKind };

    public async Task<Explanation?> ExplainAsync(
        string subjectKind, string subjectRef, CancellationToken cancellationToken)
    {
        if (!SubjectKinds.Contains(subjectKind))
        {
            return null;
        }

        // The same data-anchored analysis date the UC5 endpoints use, so the drawer and the screen it
        // opens over can never disagree on an age, a score or a recommendation.
        var settings = await inventory.BuildSettingsAsync(null, cancellationToken);
        var units = await inventory.ComputeAsync(settings, cancellationToken);

        var unit = units.FirstOrDefault(u => string.Equals(u.StockId, subjectRef, StringComparison.OrdinalIgnoreCase));
        if (unit is null)
        {
            return null;
        }

        var recommendation = unit.Recommendation;

        return new Explanation(
            Title: $"{unit.Model} {unit.Variant} · {unit.StockId}",
            Module: "Inventory Intelligence",

            // A "Low" confidence recommendation is labelled as such rather than presented with the
            // same weight as a firm one — that is what v3's `low` chip is for.
            Label: string.Equals(recommendation.Confidence, "Low", StringComparison.Ordinal)
                ? OutputLabel.LowConfidence
                : OutputLabel.Recommendation,

            Recommendation: $"{recommendation.Action}. {recommendation.Why}",
            Impacts: Impacts(unit),
            Confidence: new ConfidenceStatement(
                Band(recommendation.Confidence),

                // No percentage: the UC5 model produces a confidence *word* from the demand signal's
                // reliability, not a calibrated probability. Inventing a number to fill the slot would
                // be exactly the false precision this panel exists to remove.
                Percent: null,
                Why: [.. recommendation.Evidence, $"Demand basis: {unit.DemandDetail}"]),

            Drivers: Drivers(unit),
            Evidence: Evidence(unit),
            Assumptions: Assumptions(unit, settings),
            Lineage:
            [
                new LineageNode("Oracle Fusion — inventory (system of record)", LineageKind.Fusion),
                new LineageNode("Inventory workbook (inventory.json)", LineageKind.Workbook),
                new LineageNode("Sales workbook (sales.json)", LineageKind.Workbook),
                new LineageNode("Overstock risk model (UC5)", LineageKind.Derived),
            ],
            Model: new ModelInfo(
                Name: "Overstock & aging risk model",
                Version: "UC5 · additive weighted model",
                Recalculated: ExplanationFormat.Date(settings.AnalysisDate),
                Horizon: $"{ExplanationFormat.Count(settings.TrailingMonths)}-month trailing demand window",

                // Honest answers, not model-shaped ones. UC5 is a deterministic weighted sum; it has
                // no hold-out error because there is nothing to back-test.
                Validation: "Deterministic rule set — reproducible from the same inputs",
                Error: "rule-based"),
            // The footer searches the Decision Log by the clean "{Model} {Variant}" subject the on-screen
            // footer uses — never the "· {StockId}" display title, which no persisted recommendation holds.
            Ownership: new Ownership(
                "Inventory Manager", $"{unit.RiskBand} risk · {unit.AgingBand}", $"{unit.Model} {unit.Variant}"),
            IsDemoData: false);
    }

    private static IReadOnlyList<ImpactTile> Impacts(InventoryUnitRisk u) =>
    [
        new("Holding cost accrued", ExplanationFormat.Sar(u.AccumulatedHoldingCost), ImpactTone.Negative),
        new("Stock value", ExplanationFormat.Sar(u.PurchasePrice), ImpactTone.Neutral),
        new("Days in stock", ExplanationFormat.Days(u.InventoryAgeDays), Tone(u.AgingBand)),
        new("Risk score", $"{u.RiskScore}/100", Tone(u.RiskBand)),
    ];

    /// <summary>
    /// The risk factors, highest contribution first — which is the order the engine already returns
    /// them in, so the drawer's numbering matches the model's own ranking.
    /// </summary>
    private static IReadOnlyList<Driver> Drivers(InventoryUnitRisk u) =>
    [
        .. u.Factors.Select(f => new Driver(
            f.Label,
            $"{f.Detail} · {ExplanationFormat.Number((decimal)f.Points)} of {u.RiskScore} risk points")),
    ];

    /// <summary>
    /// The additive breakdown as a chartable series. This is the one place a UC5 explanation shows a
    /// chart, and it shows the model's own arithmetic: the bars sum, after renormalisation, to the
    /// score in the header.
    /// </summary>
    private static EvidenceSeries Evidence(InventoryUnitRisk u) => new(
        Period: "Contribution to the 0–100 risk score",
        Points: [.. u.Factors.Select(f => new EvidencePoint(f.Label, decimal.Round((decimal)f.Points, 1), null))],
        Note:
            "Weighted contributions, renormalised to a 0–100 score. A factor's weight is configurable; "
            + "the values shown are the ones this score was computed with.",
        ValueLabel: "Risk points");

    private static IReadOnlyList<string> Assumptions(InventoryUnitRisk u, RiskSettings settings) =>
    [
        .. u.Recommendation.Assumptions,
        $"Risk is scored as of {ExplanationFormat.Date(settings.AnalysisDate)}, the latest observed data "
            + "date — never the wall clock, so a frozen dataset always reproduces the same score.",
        "Scores are computed over the whole inventory set, so filtering the grid never changes a unit's score.",
        "Advisory only. BeeEye records the decision; it never writes to Oracle Fusion.",
    ];

    private static ConfidenceBand Band(string confidence) => confidence switch
    {
        "High" => ConfidenceBand.High,
        "Low" => ConfidenceBand.Low,
        _ => ConfidenceBand.Medium,
    };

    /// <summary>
    /// Maps a risk or aging band onto a tone. The words are still rendered — the tone only tints
    /// them, because status must never be carried by colour alone.
    /// </summary>
    private static ImpactTone Tone(string band) => band switch
    {
        "Critical" or "Critical aging" => ImpactTone.Negative,
        "High" or "High attention" => ImpactTone.Warning,
        "Medium" or "Watch" => ImpactTone.Warning,
        _ => ImpactTone.Neutral,
    };
}
