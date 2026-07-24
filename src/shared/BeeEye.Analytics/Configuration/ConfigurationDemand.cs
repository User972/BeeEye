using BeeEye.Analytics.Demand;

namespace BeeEye.Analytics.Configuration;

/// <summary>
/// Configuration-level demand insights (UC3): which model·variant·colour·interior
/// combinations drive demand and which are becoming dead stock. Distinguishes
/// genuine low demand (available but not selling) from stockout-suppressed demand
/// (no stock to sell) using current stock as a proxy for availability.
/// </summary>
public static class ConfigurationDemand
{
    public static string Key(string model, string variant, string colour, string interior)
        => $"{model}|{variant}|{colour}|{interior}";

    public static IReadOnlyList<ConfigDemandResult> Analyse(
        IReadOnlyList<SalesRow> sales,
        IReadOnlyDictionary<string, int> stockByConfig,
        ConfigDemandSettings settings)
    {
        if (sales.Count == 0)
        {
            return [];
        }

        // Max over a reference-typed selector is declared string? because the sequence may be empty.
        // It cannot be here — the guard above returned — and SalesRow.MonthKey is non-nullable.
        var lastMonth = sales.Max(s => s.MonthKey)!;
        var recentMonths = MonthKey.Trailing(settings.TrailingMonths, lastMonth).ToHashSet();
        var priorMonths = MonthKey.Trailing(settings.TrailingMonths, MonthKey.Add(lastMonth, -settings.TrailingMonths)).ToHashSet();

        var results = new List<ConfigDemandResult>();

        foreach (var group in sales.GroupBy(s => Key(s.Model, s.Variant, s.Colour, s.Interior)))
        {
            var rows = group.ToList();
            var first = rows[0];
            var totalUnits = rows.Sum(r => r.Units);

            var recentUnits = rows.Where(r => recentMonths.Contains(r.MonthKey)).Sum(r => r.Units);
            var priorUnits = rows.Where(r => priorMonths.Contains(r.MonthKey)).Sum(r => r.Units);
            var recentVelocity = recentUnits / settings.TrailingMonths;
            var priorVelocity = priorUnits / settings.TrailingMonths;
            var decayPct = priorVelocity > 0 ? (recentVelocity - priorVelocity) / priorVelocity * 100 : recentVelocity > 0 ? 100 : 0;
            var trend = decayPct > 8 ? "increasing" : decayPct < -8 ? "declining" : "stable";

            var rotation = recentVelocity >= settings.FastThreshold ? "Fast"
                : recentVelocity >= settings.MediumThreshold ? "Medium"
                : recentVelocity > 0 ? "Slow"
                : "Dead";

            var byRegion = rows
                .GroupBy(r => r.Location)
                .Select(g => new { Region = g.Key, Units = g.Sum(r => r.Units) })
                .OrderByDescending(x => x.Units)
                .Select(x => new RegionDemand(x.Region, x.Units, totalUnits > 0 ? x.Units / totalUnits : 0))
                .ToList();

            var monthsWithSales = rows.Select(r => r.MonthKey).Distinct().Count();
            var currentStock = stockByConfig.TryGetValue(group.Key, out var st) ? st : 0;

            results.Add(new ConfigDemandResult(
                first.Model, first.Variant, first.Colour, first.Interior,
                // A LINQ grouping always holds at least one row, so neither bound can be null.
                totalUnits, monthsWithSales, rows.Min(r => r.MonthKey)!, rows.Max(r => r.MonthKey)!,
                recentVelocity, priorVelocity, decayPct, trend, rotation,
                DecayAlert: decayPct < settings.DecayAlertPct && priorVelocity > 0,
                IsColdStart: monthsWithSales < settings.ColdStartMinMonths,
                // Sold historically but nothing recent and nothing in stock to sell => availability-suppressed.
                StockoutSuspected: recentVelocity == 0 && currentStock == 0 && totalUnits > 0,
                CurrentStock: currentStock,
                TopRegionShare: byRegion.Count > 0 ? byRegion[0].Share : 0,
                ByRegion: byRegion));
        }

        return results.OrderByDescending(r => r.TotalUnits).ToList();
    }

    public static ConfigDemandSummary Summarise(IReadOnlyList<ConfigDemandResult> configs)
    {
        int Count(string rotation) => configs.Count(c => c.RotationClass == rotation);
        double Units(string rotation) => configs.Where(c => c.RotationClass == rotation).Sum(c => c.TotalUnits);

        return new ConfigDemandSummary(
            Configurations: configs.Count,
            TotalUnits: configs.Sum(c => c.TotalUnits),
            FastCount: Count("Fast"),
            MediumCount: Count("Medium"),
            SlowCount: Count("Slow"),
            DeadCount: Count("Dead"),
            DecayAlerts: configs.Count(c => c.DecayAlert),
            ColdStart: configs.Count(c => c.IsColdStart),
            StockoutSuspected: configs.Count(c => c.StockoutSuspected),
            ByRotation:
            [
                new RotationBand("Fast", Count("Fast"), Units("Fast")),
                new RotationBand("Medium", Count("Medium"), Units("Medium")),
                new RotationBand("Slow", Count("Slow"), Units("Slow")),
                new RotationBand("Dead", Count("Dead"), Units("Dead")),
            ]);
    }
}
