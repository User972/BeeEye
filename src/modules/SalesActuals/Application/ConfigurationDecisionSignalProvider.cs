using BeeEye.Analytics.Configuration;
using BeeEye.Analytics.Decisions;
using BeeEye.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BeeEye.Modules.SalesActuals.Application;

/// <summary>
/// Raises <b>D-PRC-1 — Reduce procurement on a declining configuration</b> for the Executive
/// Decision Cockpit (UC8).
/// <para>
/// Surfaces the configuration with the largest inventory exposure among those whose recent demand
/// has decayed enough to trigger the UC3 decay alert. Impact is the stock still held for that
/// configuration valued at its own observed average selling price — money genuinely tied up behind
/// falling demand.
/// </para>
/// </summary>
public sealed class ConfigurationDecisionSignalProvider(ConfigurationReadService configurations, BeeEyeDbContext db)
    : IDecisionSignalProvider
{
    /// <summary>Decay at or beyond this percentage is treated as severe rather than a watch item.</summary>
    private const double SevereDecayPct = -60.0;

    public string Area => "Procurement";

    public async Task<IReadOnlyList<Decision>> GetDecisionsAsync(CancellationToken cancellationToken)
    {
        var configs = await configurations.AnalyseAsync(ConfigDemandSettings.Default, cancellationToken);

        // Only configurations that are both declining and still holding stock represent money at risk.
        var alerts = configs.Where(c => c is { DecayAlert: true, CurrentStock: > 0 }).ToList();
        if (alerts.Count == 0)
        {
            return [];
        }

        var prices = await AveragePricesAsync(cancellationToken);

        var best = alerts
            .Select(c => new { Config = c, Exposure = c.CurrentStock * Price(prices, c.Model, c.Variant) })
            .OrderByDescending(x => x.Exposure)
            .ThenBy(x => x.Config.DecayPct)
            .ThenBy(x => x.Config.Model, StringComparer.Ordinal)
            .First();

        var config = best.Config;
        var severe = config.DecayPct <= SevereDecayPct;

        return
        [
            new Decision(
                Id: "D-PRC-1",
                Title: $"Reduce procurement — {config.Model} {config.Variant} · {config.Colour}",
                Area: Area,
                Screen: "configuration-demand",
                Severity: severe ? DecisionSeverity.High : DecisionSeverity.Medium,
                ImpactSar: best.Exposure,
                Kind: DecisionKind.Risk,
                Confidence01: severe ? 0.7 : 0.6,
                WhyNow:
                    $"Recent demand is {config.DecayPct:0.#}% against the prior period ({config.TrendDirection}), "
                    + $"with {config.CurrentStock} units still in stock.",
                Action: "Reduce the next procurement round and review promotion or redistribution.",
                Evidence:
                    $"{config.RotationClass} rotation · recent velocity {config.RecentVelocity:0.##}/month "
                    + $"vs prior {config.PriorVelocity:0.##}/month",
                OwnerRole: "Procurement Manager",
                Urgency: 0.7,
                Controllability: 0.9),
        ];
    }

    /// <summary>Units-weighted average selling price per model · variant, computed in the database.</summary>
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
