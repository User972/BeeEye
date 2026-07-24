using BeeEye.Analytics.Explainability;
using BeeEye.Modules.Recommendations.Contracts;

namespace BeeEye.Modules.Recommendations.Application;

/// <summary>
/// Explains one model·variant's order recommendation (UC1, V3-DS-006).
/// <para>
/// The sparse counterpart to <c>InventoryExplainabilityProvider</c>: UC1 separates the demand
/// forecast from the business constraints, and the drivers are exactly that separation — forecast
/// demand, safety stock, what is already available, and what the constraints did to the raw net
/// requirement. There is <b>no evidence series</b>: the UC1 screen shows no per-configuration chart,
/// so the drawer omits the section rather than drawing one from nothing.
/// </para>
/// </summary>
public sealed class OrderExplainabilityProvider(OrderReadService orders) : IExplainabilityProvider
{
    /// <summary>The subject is a model·variant, referenced as <c>"{Model}|{Variant}"</c>.</summary>
    public const string OrderConfigurationKind = "order-configuration";

    public IReadOnlySet<string> SubjectKinds { get; } =
        new HashSet<string>(StringComparer.Ordinal) { OrderConfigurationKind };

    public async Task<Explanation?> ExplainAsync(
        string subjectKind, string subjectRef, CancellationToken cancellationToken)
    {
        if (!SubjectKinds.Contains(subjectKind))
        {
            return null;
        }

        // The default planning scenario, the same one the cockpit uses. Analysts tune scenarios on the
        // UC1 screen; explaining the tuned figure would need the scenario in the subject reference,
        // which is deliberately deferred rather than half-done — see the S3 notes.
        var scenario = OrderScenario.From(null, null, null, null, null, null, null);
        var rows = await orders.RecommendAsync(scenario, cancellationToken);

        var row = rows.FirstOrDefault(r =>
            string.Equals($"{r.Model}|{r.Variant}", subjectRef, StringComparison.OrdinalIgnoreCase));
        if (row is null)
        {
            return null;
        }

        var prices = await orders.AverageSellingPricesAsync(cancellationToken);
        var price = prices.TryGetValue((row.Model, row.Variant), out var p) ? p : 0m;

        return new Explanation(
            Title: $"{row.Model} {row.Variant}",
            Module: "Order Optimisation",
            Label: string.Equals(row.Confidence, "Low", StringComparison.Ordinal)
                ? OutputLabel.LowConfidence
                : OutputLabel.Recommendation,
            Recommendation:
                $"Order about {ExplanationFormat.Count(row.RecommendedQuantity)} units. {row.Rationale}",
            Impacts:
            [
                new("Recommended order", $"{ExplanationFormat.Count(row.RecommendedQuantity)} units",
                    row.RecommendedQuantity > 0 ? ImpactTone.Positive : ImpactTone.Neutral),
                new("Net requirement", $"{ExplanationFormat.Count(row.NetRequirement)} units",
                    row.NetRequirement > 0 ? ImpactTone.Warning : ImpactTone.Neutral),
                new("Revenue at stake", ExplanationFormat.Sar(row.NetRequirement * price),
                    ImpactTone.Positive),
                new("Understock risk", row.UnderstockRisk, Tone(row.UnderstockRisk)),
            ],
            Confidence: new ConfidenceStatement(
                Band(row.Confidence),

                // The band is derived from the forecaster's measured wMAPE, so unlike UC5 there *is* a
                // number behind it — reported as the error itself rather than dressed up as a
                // probability the model never produced.
                Percent: null,
                Why: Why(row, scenario)),
            Drivers:
            [
                new($"Forecast demand over {scenario.Horizon} months",
                    $"{ExplanationFormat.Number((decimal)row.ForecastDemand)} units · {row.ChosenModel}"),
                new("Safety stock for the target cover",
                    $"{ExplanationFormat.Number((decimal)row.SafetyStock)} units at "
                    + $"{ExplanationFormat.Number((decimal)scenario.TargetCoverMonths)} months' cover"),
                new("Already available",
                    $"{ExplanationFormat.Count(row.Available)} units — stock, inbound and confirmed orders"),
                new("Recent monthly velocity",
                    $"{ExplanationFormat.Number((decimal)row.MonthlyVelocity)} units per month, last 3 months"),
                new("Ordering constraints applied",
                    $"minimum {ExplanationFormat.Count(scenario.MinOrderQuantity)} · multiples of "
                    + $"{ExplanationFormat.Count(scenario.OrderMultiple)}"
                    + (scenario.AllocationLimit is { } limit
                        ? $" · allocation capped at {ExplanationFormat.Count(limit)}"
                        : " · no allocation cap")),
            ],

            // Deliberately none. The UC1 screen has no per-configuration history chart to reuse, and a
            // placeholder would imply evidence that is not being shown.
            Evidence: null,
            Assumptions:
            [
                $"Planning horizon of {scenario.Horizon} months at "
                    + $"{ExplanationFormat.Number((decimal)scenario.TargetCoverMonths)} months' target cover — "
                    + "the default scenario, not a tuned one.",
                "Revenue at stake values the shortfall at this configuration's units-weighted average "
                    + "selling price observed in sales history; it is not a quoted or contracted price.",
                "Supplier capacity, allocation politics and lead-time variability are not modelled.",
                "Advisory only. A human places the order in Oracle Fusion; BeeEye never writes to it.",
            ],
            Lineage:
            [
                new LineageNode("Oracle Fusion Order Management (system of record)", LineageKind.Fusion),
                new LineageNode("Sales workbook (sales.json)", LineageKind.Workbook),
                new LineageNode("Inventory workbook (inventory.json)", LineageKind.Workbook),
                new LineageNode("Demand forecaster + order optimiser (UC1)", LineageKind.Derived),
            ],
            Model: new ModelInfo(
                Name: $"Demand forecast: {row.ChosenModel}",
                Version: "UC1 · forecaster + constraint optimiser",
                Recalculated: "On request — computed live from the current dataset",
                Horizon: $"{scenario.Horizon} months",
                Validation: "6-month hold-out back-test",
                Error: row.Wmape is { } wmape
                    ? $"wMAPE {ExplanationFormat.Percent((decimal)wmape)}"
                    : "not measurable — history too short to back-test"),
            Ownership: new Ownership("Sales Planning Manager", $"Understock risk {row.UnderstockRisk}"),
            IsDemoData: false);
    }

    private static IReadOnlyList<string> Why(OrderRecommendationRow row, OrderScenario scenario)
    {
        var why = new List<string>
        {
            row.Wmape is { } wmape
                ? $"Back-tested forecast error is {ExplanationFormat.Percent((decimal)wmape)} wMAPE "
                  + $"using {row.ChosenModel}."
                : "The history is too short to back-test, so the forecast carries no measured error.",
            $"Demand is projected over {scenario.Horizon} months and compared against "
              + $"{ExplanationFormat.Count(row.Available)} available units.",
        };

        if (row.MonthlyVelocity <= 0)
        {
            why.Add("No units have sold in the recent window, so the velocity behind this figure is zero.");
        }

        return why;
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
