using BeeEye.Analytics.Explainability;
using BeeEye.Modules.Procurement.Contracts;

namespace BeeEye.Modules.Procurement.Application;

/// <summary>
/// Explains one model·variant's procurement recommendation (UC4, V3-DS-006).
/// <para>
/// UC4 is the use case with the largest gap between what it computes and what it can observe, and the
/// explanation says so rather than hiding it. There is <b>no supplier, purchase-order or
/// delivery-performance entity in the database</b> (V3-CONFLICT-9): lead time is inferred from
/// inventory records, its variability is a documented default where inventory carries none, and no
/// supplier on-time history exists at all. Those are assumptions, and they are listed as assumptions
/// — the alternative is a safety stock that looks measured and is not.
/// </para>
/// </summary>
public sealed class ProcurementExplainabilityProvider(ProcurementReadService procurement)
    : IExplainabilityProvider
{
    /// <summary>The subject is a model·variant, referenced as <c>"{Model}|{Variant}"</c>.</summary>
    public const string ProcurementItemKind = "procurement-item";

    public IReadOnlySet<string> SubjectKinds { get; } =
        new HashSet<string>(StringComparer.Ordinal) { ProcurementItemKind };

    public async Task<Explanation?> ExplainAsync(
        string subjectKind, string subjectRef, CancellationToken cancellationToken)
    {
        if (!SubjectKinds.Contains(subjectKind))
        {
            return null;
        }

        // The default scenario, matching the screen's defaults. Analysts tune service level and
        // review period on the UC4 screen; explaining a tuned figure would need the scenario in the
        // subject reference, which is deferred rather than half-done.
        var scenario = ProcurementScenario.From(null, null, null, null, null, null);
        var rows = await procurement.RecommendAsync(scenario, cancellationToken);

        var row = rows.FirstOrDefault(r =>
            string.Equals($"{r.Model}|{r.Variant}", subjectRef, StringComparison.OrdinalIgnoreCase));
        if (row is null)
        {
            return null;
        }

        var variability = row.DemandMean > 0 ? row.DemandStd / row.DemandMean : 0;

        return new Explanation(
            Title: $"{row.Model} {row.Variant}",
            Module: "Procurement Optimisation",
            Label: string.Equals(row.Confidence, "Low", StringComparison.Ordinal)
                ? OutputLabel.LowConfidence
                : OutputLabel.Recommendation,
            Recommendation:
                $"Order about {ExplanationFormat.Count(row.RecommendedQuantity)} units "
                + $"(range {ExplanationFormat.Count(row.RangeLow)}–{ExplanationFormat.Count(row.RangeHigh)}). "
                + row.Rationale,
            Impacts:
            [
                new("Recommended quantity", $"{ExplanationFormat.Count(row.RecommendedQuantity)} units",
                    row.RecommendedQuantity > 0 ? ImpactTone.Positive : ImpactTone.Neutral),
                new("Reorder point", $"{ExplanationFormat.Number((decimal)row.ReorderPoint)} units",
                    ImpactTone.Neutral),
                new("Safety stock", $"{ExplanationFormat.Number((decimal)row.SafetyStock)} units",
                    ImpactTone.Neutral),
                new("Stockout risk", row.StockoutRisk, Tone(row.StockoutRisk)),
            ],
            Confidence: new ConfidenceStatement(
                Band(row.Confidence),

                // The service level is a *target*, not a confidence, so it is not reported as one.
                // It appears in the drivers and the assumptions where it belongs.
                Percent: null,
                Why:
                [
                    $"Monthly demand averages {ExplanationFormat.Number((decimal)row.DemandMean)} units with a "
                        + $"standard deviation of {ExplanationFormat.Number((decimal)row.DemandStd)} "
                        + $"(coefficient of variation {ExplanationFormat.Number((decimal)variability, 2)}).",
                    variability < 0.5
                        ? "Demand is stable relative to its mean, so the safety stock is a reliable buffer."
                        : "Demand is volatile relative to its mean, so the buffer is wide and the "
                          + "recommendation is correspondingly less precise.",
                    "Lead time is inferred from inventory records. No supplier delivery history exists to "
                        + "validate it against.",
                ]),
            Drivers:
            [
                new("Mean monthly demand",
                    $"{ExplanationFormat.Number((decimal)row.DemandMean)} units/month over the active history"),
                new("Demand variability",
                    $"σ {ExplanationFormat.Number((decimal)row.DemandStd)} units · CV "
                    + ExplanationFormat.Number((decimal)variability, 2)),
                new("Lead time",
                    $"{ExplanationFormat.Number((decimal)row.LeadTimeMonths, 2)} months, from inventory records"),
                new($"Service level target {ExplanationFormat.Percent((decimal)(scenario.ServiceLevel * 100), 0)}",
                    $"drives {ExplanationFormat.Number((decimal)row.SafetyStock)} units of safety stock"),
                new("Order-up-to level",
                    $"{ExplanationFormat.Number((decimal)row.OrderUpToLevel)} units against "
                    + $"{ExplanationFormat.Count(row.Available)} available"),
            ],

            // No chart on the UC4 screen to reuse, so no evidence section.
            Evidence: null,
            Assumptions:
            [
                $"Service level of {ExplanationFormat.Percent((decimal)(scenario.ServiceLevel * 100), 0)} over a "
                    + $"{ExplanationFormat.Number((decimal)scenario.ReviewPeriodMonths)}-month review period — "
                    + "the default scenario, not an ADMC-confirmed policy.",
                "Lead time is inferred from the average recorded on inventory items for this "
                    + "configuration. Where inventory carries none, two months is assumed.",
                "Lead-time variability defaults to half a month where inventory records no spread.",
                "No supplier, purchase-order or delivery-performance data exists in the platform, so "
                    + "supplier on-time reliability is not a factor in this figure at all "
                    + "(V3-CONFLICT-9).",
                "Demand is assumed approximately normal for the safety-stock calculation, which "
                    + "understates the buffer for demand that arrives in lumps.",
                "Advisory only. A human raises the purchase order; BeeEye never writes to Oracle Fusion.",
            ],
            Lineage:
            [
                new LineageNode("Oracle Fusion — sales (system of record)", LineageKind.Fusion),
                new LineageNode("Sales workbook (sales.json)", LineageKind.Workbook),
                new LineageNode("Inventory workbook — lead times (inventory.json)", LineageKind.Workbook),

                // Named as demo so a reader is never left assuming a supplier feed exists. It does
                // not, and the safety stock is the poorer for it.
                new LineageNode("Supplier & PO history — not integrated", LineageKind.Demo),
                new LineageNode("Procurement optimiser (UC4)", LineageKind.Derived),
            ],
            Model: new ModelInfo(
                Name: "Periodic-review (R, S) procurement model",
                Version: "UC4 · normal-demand safety stock",
                Recalculated: "On request — computed live from the current dataset",
                Horizon: $"{ExplanationFormat.Number((decimal)row.LeadTimeMonths, 2)}-month lead time + "
                    + $"{ExplanationFormat.Number((decimal)scenario.ReviewPeriodMonths)}-month review",
                Validation: "Deterministic rule set — reproducible from the same inputs",
                Error: "rule-based"),
            Ownership: new Ownership("Procurement Manager", $"Stockout risk {row.StockoutRisk}"),

            // The recommendation itself is computed from real sales and real inventory lead times.
            // The missing supplier feed is a stated gap, not synthetic data standing in for one, so
            // this is not demo data.
            IsDemoData: false);
    }

    private static ConfidenceBand Band(string confidence) => confidence switch
    {
        "High" => ConfidenceBand.High,
        "Low" => ConfidenceBand.Low,
        _ => ConfidenceBand.Medium,
    };

    private static ImpactTone Tone(string risk) => risk switch
    {
        "High" => ImpactTone.Negative,
        "Medium" => ImpactTone.Warning,
        _ => ImpactTone.Neutral,
    };
}
