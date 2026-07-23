using BeeEye.Analytics.Decisions;
using BeeEye.Modules.Recommendations.Contracts;
using BeeEye.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BeeEye.Modules.Recommendations.Application;

/// <summary>
/// Raises <b>D-ORD-1 — Increase order allocation</b> for the Executive Decision Cockpit (UC8).
/// <para>
/// Surfaces the configuration whose next-month order shortfall carries the largest revenue
/// exposure: demand the current plus inbound supply will not cover. Monetary impact is the net
/// requirement valued at the configuration's own observed average selling price, so the figure is
/// derived from real sales history rather than assumed.
/// </para>
/// </summary>
public sealed class OrderDecisionSignalProvider(OrderReadService orders, BeeEyeDbContext db)
    : IDecisionSignalProvider
{
    /// <summary>Only configurations needing at least this many units are worth an executive decision.</summary>
    private const int MinimumNetRequirement = 1;

    public string Area => "Order Planning";

    public async Task<IReadOnlyList<Decision>> GetDecisionsAsync(CancellationToken cancellationToken)
    {
        // The cockpit uses the default planning scenario; analysts tune scenarios on the UC1 screen.
        var scenario = OrderScenario.From(null, null, null, null, null, null, null);
        var rows = await orders.RecommendAsync(scenario, cancellationToken);

        var candidates = rows.Where(r => r.NetRequirement >= MinimumNetRequirement).ToList();
        if (candidates.Count == 0)
        {
            return [];
        }

        var prices = await AveragePricesAsync(cancellationToken);

        var best = candidates
            .Select(r => new { Row = r, Exposure = r.NetRequirement * Price(prices, r.Model, r.Variant) })
            .OrderByDescending(x => x.Exposure)
            .ThenByDescending(x => x.Row.NetRequirement)
            .ThenBy(x => x.Row.Model, StringComparer.Ordinal)
            .First();

        var row = best.Row;
        var urgent = string.Equals(row.UnderstockRisk, "High", StringComparison.Ordinal);

        return
        [
            new Decision(
                Id: "D-ORD-1",
                Title: $"Increase order allocation — {row.Model} {row.Variant}",
                Area: Area,
                Screen: "order-optimisation",
                Severity: urgent ? DecisionSeverity.High : DecisionSeverity.Medium,
                ImpactSar: best.Exposure,
                Kind: DecisionKind.Opportunity,
                Confidence01: DecisionPriority.ConfidenceWeight(row.Confidence),
                WhyNow:
                    $"Forecast demand of {row.ForecastDemand:0} units over the next {scenario.Horizon} months, "
                    + $"against {row.Available} available, leaves a net requirement of {row.NetRequirement}; "
                    + $"understock risk is {row.UnderstockRisk.ToLowerInvariant()}.",
                Action: $"Prepare an order proposal for about {row.RecommendedQuantity} units.",
                Evidence:
                    $"{row.NetRequirement} units short over {scenario.Horizon} months · monthly velocity "
                    + $"{row.MonthlyVelocity:0.#} · forecast by {row.ChosenModel}",
                OwnerRole: "Sales Planning Manager",
                Urgency: urgent ? 0.85 : 0.6,
                Controllability: 0.8),
        ];
    }

    /// <summary>
    /// Observed average selling price per model · variant. Computed in the database rather than by
    /// materialising sales history, and weighted by units so high-volume months dominate — the same
    /// basis the UC1 screen values an order at.
    /// </summary>
    private async Task<Dictionary<(string Model, string Variant), decimal>> AveragePricesAsync(
        CancellationToken cancellationToken)
    {
        var rows = await db.SalesFacts
            .AsNoTracking()
            .Where(f => f.UnitsSold > 0)
            .GroupBy(f => new { f.Model, f.Variant })
            .Select(g => new
            {
                g.Key.Model,
                g.Key.Variant,
                Revenue = g.Sum(f => f.Revenue),
                Units = g.Sum(f => f.UnitsSold),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Where(r => r.Units > 0)
            .ToDictionary(r => (r.Model, r.Variant), r => r.Revenue / r.Units);
    }

    private static decimal Price(
        IReadOnlyDictionary<(string Model, string Variant), decimal> prices, string model, string variant) =>
        prices.TryGetValue((model, variant), out var price) ? price : 0m;
}
