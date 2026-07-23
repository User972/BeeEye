using BeeEye.Analytics.Decisions;
using BeeEye.Analytics.Inventory;

namespace BeeEye.Modules.Inventory.Application;

/// <summary>
/// Raises <b>D-INV-1 — Redistribute aging inventory</b> for the Executive Decision Cockpit (UC8).
/// <para>
/// Groups the units the UC5 engine recommends transferring and surfaces the model · variant with the
/// greatest holding cost still accruing. Impact is the annual holding cost that redistribution would
/// stop paying — computed from each unit's own <c>HoldingCostPerDay</c>, so no rate is assumed.
/// </para>
/// </summary>
public sealed class InventoryDecisionSignalProvider(InventoryReadService inventory) : IDecisionSignalProvider
{
    /// <summary>The UC5 recommendation action this decision is raised from.</summary>
    private const string TransferAction = "Transfer stock";

    private const int DaysPerYear = 365;

    /// <summary>Above this many units in one group the situation is treated as severe.</summary>
    private const int SevereGroupSize = 5;

    public string Area => "Inventory";

    public async Task<IReadOnlyList<Decision>> GetDecisionsAsync(CancellationToken cancellationToken)
    {
        var units = await inventory.ComputeAsync(RiskSettings.Default, cancellationToken);

        var transfers = units
            .Where(u => string.Equals(u.Recommendation.Action, TransferAction, StringComparison.Ordinal))
            .ToList();

        if (transfers.Count == 0)
        {
            return [];
        }

        var best = transfers
            .GroupBy(u => (u.Model, u.Variant, u.Location))
            .Select(g => new
            {
                g.Key.Model,
                g.Key.Variant,
                Source = g.Key.Location,
                Units = g.Count(),
                AnnualHolding = g.Sum(u => u.HoldingCostPerDay) * DaysPerYear,
                Value = g.Sum(u => u.PurchasePrice),
                Destination = g.Select(u => u.Recommendation.Destination).FirstOrDefault(d => d is not null),
                Confidence = g.Select(u => u.Recommendation.Confidence).First(),
                OldestAgeDays = g.Max(u => u.InventoryAgeDays),
            })
            .OrderByDescending(x => x.AnnualHolding)
            .ThenByDescending(x => x.Units)
            .ThenBy(x => x.Model, StringComparer.Ordinal)
            .First();

        var severe = best.Units >= SevereGroupSize;
        var destination = best.Destination is null ? "a location with live demand" : best.Destination;

        return
        [
            new Decision(
                Id: "D-INV-1",
                Title: $"Redistribute aging inventory — {best.Model} {best.Variant}",
                Area: Area,
                Screen: "inventory-intelligence",
                Severity: severe ? DecisionSeverity.High : DecisionSeverity.Medium,
                ImpactSar: best.AnnualHolding,
                Kind: DecisionKind.Risk,
                Confidence01: DecisionPriority.ConfidenceWeight(best.Confidence),
                WhyNow:
                    $"{best.Source} holds {best.Units} unit(s) the risk model recommends transferring; "
                    + $"the oldest has been in stock {best.OldestAgeDays} days.",
                Action: $"Prepare a transfer recommendation: {best.Units} unit(s) {best.Source} → {destination}.",
                Evidence:
                    $"{best.Units} unit(s) · {best.Value:N0} SAR stock value · "
                    + $"{best.AnnualHolding:N0} SAR annual holding cost",
                OwnerRole: "Inventory Manager",
                Urgency: 0.6,
                Controllability: 0.85),
        ];
    }
}
