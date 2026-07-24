using BeeEye.Analytics.Inventory;

namespace BeeEye.Modules.Inventory.Contracts;

/// <summary>Server-side filter for UC5 queries. Empty arrays mean "no restriction".</summary>
public sealed record InventoryFilter(
    IReadOnlyList<string> Brand,
    IReadOnlyList<string> Model,
    IReadOnlyList<string> Variant,
    IReadOnlyList<string> Type,
    IReadOnlyList<string> Location,
    IReadOnlyList<string> Colour,
    IReadOnlyList<string> Interior,
    IReadOnlyList<string> RiskBand)
{
    public bool Matches(InventoryUnitRisk u)
        => In(Brand, u.Brand) && In(Model, u.Model) && In(Variant, u.Variant) && In(Type, u.Type)
           && In(Location, u.Location) && In(Colour, u.Colour) && In(Interior, u.Interior)
           && In(RiskBand, u.RiskBand);

    private static bool In(IReadOnlyList<string> allowed, string value)
        => allowed.Count == 0 || allowed.Contains(value, StringComparer.OrdinalIgnoreCase);

    public static InventoryFilter From(
        string[]? brand, string[]? model, string[]? variant, string[]? type,
        string[]? location, string[]? colour, string[]? interior, string[]? riskBand)
        => new(brand ?? [], model ?? [], variant ?? [], type ?? [], location ?? [], colour ?? [], interior ?? [], riskBand ?? []);
}

/// <summary>A single row in the inventory grid — a light projection of the full risk record.</summary>
public sealed record InventoryItemRow(
    string StockId,
    string Brand,
    string Model,
    string Variant,
    string Colour,
    string Location,
    int InventoryAgeDays,
    string AgingBand,
    double Velocity,
    double StockCover,
    string TrendDirection,
    int RiskScore,
    string RiskBand,
    decimal PurchasePrice,
    decimal AccumulatedHoldingCost,
    string RecommendedAction);

/// <summary>Metadata attached to every UC5 response (freshness/config disclosure).</summary>
public sealed record InventoryMeta(DateOnly AnalysisDate, int TotalUnits, int FilteredUnits, DateTimeOffset GeneratedAtUtc);

public sealed record InventorySummaryResponse(InventorySummary Summary, InventoryMeta Meta);

public sealed record InventoryItemsResponse(IReadOnlyList<InventoryItemRow> Items, int Page, int PageSize, long TotalCount, InventoryMeta Meta);

public sealed record FilterOptions(
    IReadOnlyList<string> Brands,
    IReadOnlyList<string> Models,
    IReadOnlyList<string> Variants,
    IReadOnlyList<string> Types,
    IReadOnlyList<string> Locations,
    IReadOnlyList<string> Colours,
    IReadOnlyList<string> Interiors,
    IReadOnlyList<string> RiskBands);

public static class InventoryMapping
{
    public static InventoryItemRow ToRow(this InventoryUnitRisk u) => new(
        u.StockId, u.Brand, u.Model, u.Variant, u.Colour, u.Location, u.InventoryAgeDays, u.AgingBand,
        u.Velocity, u.StockCover, u.TrendDirection, u.RiskScore, u.RiskBand, u.PurchasePrice,
        u.AccumulatedHoldingCost, u.Recommendation.Action);

    public static IReadOnlyList<InventoryUnitRisk> Sort(this IEnumerable<InventoryUnitRisk> units, string? sort)
        // StockId is a stable, unique tie-breaker so paging is deterministic across requests
        // (the underlying query has no ORDER BY, so ties would otherwise resolve to DB heap order).
        => (sort?.ToLowerInvariant()) switch
        {
            "age" => units.OrderByDescending(u => u.InventoryAgeDays).ThenBy(u => u.StockId, StringComparer.Ordinal).ToList(),
            "cover" => units.OrderByDescending(u => u.StockCover).ThenBy(u => u.StockId, StringComparer.Ordinal).ToList(),
            "value" => units.OrderByDescending(u => u.PurchasePrice).ThenBy(u => u.StockId, StringComparer.Ordinal).ToList(),
            "holding" => units.OrderByDescending(u => u.AccumulatedHoldingCost).ThenBy(u => u.StockId, StringComparer.Ordinal).ToList(),
            _ => units.OrderByDescending(u => u.RiskScore).ThenBy(u => u.StockId, StringComparer.Ordinal).ToList(), // default: riskiest first
        };
}
