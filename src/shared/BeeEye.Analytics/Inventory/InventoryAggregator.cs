namespace BeeEye.Analytics.Inventory;

public sealed record BandValue(string Key, int Units, decimal Value);

public sealed record DimensionValue(string Key, int Units, decimal Value, decimal AccumulatedHoldingCost);

/// <summary>Portfolio-level UC5 aggregates over a (possibly filtered) set of scored units.</summary>
public sealed record InventorySummary(
    int Count,
    decimal Value,
    decimal AccumulatedHoldingCost,
    decimal DailyHoldingCost,
    double AverageInventoryAge,
    double AverageManufacturingAge,
    double AverageLeadTime,
    decimal HighRiskValue,
    decimal CriticalValue,
    int CriticalCount,
    int HighCount,
    decimal DecliningValue,
    int TransferCount,
    int PromotionCount,
    int DiscountCount,
    int PauseCount,
    IReadOnlyList<BandValue> ByRisk,
    IReadOnlyList<BandValue> ByAging,
    IReadOnlyList<BandValue> ByManufacturing,
    IReadOnlyList<DimensionValue> ByLocation,
    IReadOnlyList<DimensionValue> ByModel,
    IReadOnlyList<DimensionValue> ByVariant,
    IReadOnlyList<DimensionValue> ByBrand,
    IReadOnlyList<DimensionValue> ByColour,
    IReadOnlyList<DimensionValue> ByInterior);

public static class InventoryAggregator
{
    private static readonly string[] RiskKeys = ["Low", "Medium", "High", "Critical"];
    private static readonly string[] AgingKeys = ["New", "Healthy", "Watch", "High attention", "Critical aging"];
    private static readonly string[] MfgKeys = ["0–180 days", "181–270 days", "271–365 days", "365+ days"];

    public static InventorySummary Aggregate(IReadOnlyList<InventoryUnitRisk> units)
    {
        return new InventorySummary(
            Count: units.Count,
            Value: units.Sum(u => u.PurchasePrice),
            AccumulatedHoldingCost: units.Sum(u => u.AccumulatedHoldingCost),
            DailyHoldingCost: units.Sum(u => u.HoldingCostPerDay),
            AverageInventoryAge: Mean(units, u => u.InventoryAgeDays),
            AverageManufacturingAge: Mean(units, u => u.ManufacturingAgeDays),
            AverageLeadTime: Mean(units, u => u.LeadTimeDays),
            HighRiskValue: units.Where(u => u.RiskBand is "High" or "Critical").Sum(u => u.PurchasePrice),
            CriticalValue: units.Where(u => u.RiskBand == "Critical").Sum(u => u.PurchasePrice),
            CriticalCount: units.Count(u => u.RiskBand == "Critical"),
            HighCount: units.Count(u => u.RiskBand == "High"),
            DecliningValue: units.Where(u => u.TrendDirection == "declining").Sum(u => u.PurchasePrice),
            TransferCount: units.Count(u => u.Recommendation.Action == "Transfer stock"),
            PromotionCount: units.Count(u => u.Recommendation.Action == "Start targeted promotion"),
            DiscountCount: units.Count(u => u.Recommendation.Action == "Apply controlled discount"),
            PauseCount: units.Count(u => u.Recommendation.Action == "Pause / reduce procurement"),
            ByRisk: RiskKeys.Select(k => Band(units, u => u.RiskBand, k)).ToList(),
            ByAging: AgingKeys.Select(k => Band(units, u => u.AgingBand, k)).ToList(),
            ByManufacturing: MfgKeys.Select(k => Band(units, u => u.ManufacturingBand, k)).ToList(),
            ByLocation: Dim(units, u => u.Location),
            ByModel: Dim(units, u => u.Model),
            ByVariant: Dim(units, u => u.Variant),
            ByBrand: Dim(units, u => u.Brand),
            ByColour: Dim(units, u => u.Colour),
            ByInterior: Dim(units, u => u.Interior));
    }

    private static double Mean(IReadOnlyList<InventoryUnitRisk> units, Func<InventoryUnitRisk, double> selector)
        => units.Count == 0 ? 0 : units.Average(selector);

    private static BandValue Band(IReadOnlyList<InventoryUnitRisk> units, Func<InventoryUnitRisk, string> key, string k)
    {
        var matched = units.Where(u => key(u) == k).ToList();
        return new BandValue(k, matched.Count, matched.Sum(u => u.PurchasePrice));
    }

    private static IReadOnlyList<DimensionValue> Dim(IReadOnlyList<InventoryUnitRisk> units, Func<InventoryUnitRisk, string> key)
        => units.GroupBy(key)
            .Select(g => new DimensionValue(g.Key, g.Count(), g.Sum(u => u.PurchasePrice), g.Sum(u => u.AccumulatedHoldingCost)))
            .OrderByDescending(d => d.Value)
            .ToList();
}
