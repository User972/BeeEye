using BeeEye.Analytics.Decisions;
using BeeEye.Analytics.SpareParts;
using BeeEye.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BeeEye.Modules.SpareParts.Application;

/// <summary>
/// Raises <b>D-PRT-1 — Increase spare-parts stock</b> for the Executive Decision Cockpit (UC8).
/// <para>
/// Surfaces the part carrying the greatest reorder value across locations that the UC7 intermittent
/// -demand model flags at high stockout risk. Impact is the recommended quantity valued at the
/// part's catalogue <c>UnitCost</c>.
/// </para>
/// <para>
/// The spare-parts catalogue and usage history are <b>synthetic demo data</b>, so every decision this
/// provider raises is marked <see cref="Decision.IsDemo"/> and must be labelled as such wherever it
/// is displayed.
/// </para>
/// </summary>
public sealed class SparePartsDecisionSignalProvider(SparePartsReadService parts, BeeEyeDbContext db)
    : IDecisionSignalProvider
{
    private const string HighStockoutRisk = "High";

    /// <summary>Exposure at or above this many affected locations is treated as severe.</summary>
    private const int SevereLocationCount = 3;

    public string Area => "Parts";

    public async Task<IReadOnlyList<Decision>> GetDecisionsAsync(CancellationToken cancellationToken)
    {
        var results = await parts.RecommendAllAsync(new SparePartsSettings(), cancellationToken);

        var atRisk = results
            .Where(p => string.Equals(p.Recommendation.StockoutRisk, HighStockoutRisk, StringComparison.Ordinal))
            .Where(p => p.Recommendation.RecommendedQuantity is > 0)
            .ToList();

        if (atRisk.Count == 0)
        {
            return [];
        }

        var costs = await db.Parts
            .AsNoTracking()
            .Select(p => new { p.PartNumber, p.UnitCost })
            .ToDictionaryAsync(p => p.PartNumber, p => p.UnitCost, cancellationToken);

        var best = atRisk
            .GroupBy(p => p.PartNumber, StringComparer.Ordinal)
            .Select(g => new
            {
                PartNumber = g.Key,
                Name = g.First().Name,
                Category = g.First().Category,
                Locations = g.Select(p => p.Location).Distinct(StringComparer.Ordinal).Count(),
                Quantity = g.Sum(p => p.Recommendation.RecommendedQuantity ?? 0),
                Confidence = g.First().Recommendation.Confidence,
                Method = g.First().Recommendation.Method,
                Available = g.Sum(p => p.Recommendation.Available),
            })
            .Select(x => new { X = x, Value = x.Quantity * Cost(costs, x.PartNumber) })
            .OrderByDescending(x => x.Value)
            .ThenByDescending(x => x.X.Locations)
            .ThenBy(x => x.X.PartNumber, StringComparer.Ordinal)
            .First();

        var part = best.X;
        var severe = part.Locations >= SevereLocationCount;

        return
        [
            new Decision(
                Id: "D-PRT-1",
                Title: $"Increase stock for {part.Name} ({part.PartNumber})",
                Area: Area,
                Screen: "spare-parts",
                Severity: severe ? DecisionSeverity.High : DecisionSeverity.Medium,
                ImpactSar: best.Value,
                Kind: DecisionKind.Risk,
                Confidence01: DecisionPriority.ConfidenceWeight(part.Confidence),
                WhyNow:
                    $"{part.Locations} location(s) are below their reorder point on a {part.Category} part, "
                    + $"with {part.Available} unit(s) available against a recommended {part.Quantity}.",
                Action: "Reorder now and lift safety stock at the exposed branches.",
                Evidence:
                    $"{part.Locations} location(s) at risk · {part.Quantity} unit(s) recommended · "
                    + $"forecast by {part.Method}",
                OwnerRole: "Parts Manager",
                Urgency: severe ? 0.75 : 0.6,
                Controllability: 0.75,
                IsDemo: true),
        ];
    }

    private static decimal Cost(IReadOnlyDictionary<string, decimal> costs, string partNumber) =>
        costs.TryGetValue(partNumber, out var cost) ? cost : 0m;
}
