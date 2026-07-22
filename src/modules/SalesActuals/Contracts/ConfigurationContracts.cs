using BeeEye.Analytics.Configuration;

namespace BeeEye.Modules.SalesActuals.Contracts;

public sealed record ConfigFilter(
    IReadOnlyList<string> Model,
    IReadOnlyList<string> Variant,
    IReadOnlyList<string> Colour,
    IReadOnlyList<string> Interior,
    IReadOnlyList<string> Rotation)
{
    public bool Matches(ConfigDemandResult c)
        => In(Model, c.Model) && In(Variant, c.Variant) && In(Colour, c.Colour) && In(Interior, c.Interior)
           && In(Rotation, c.RotationClass);

    private static bool In(IReadOnlyList<string> allowed, string value)
        => allowed.Count == 0 || allowed.Contains(value, StringComparer.OrdinalIgnoreCase);

    public static ConfigFilter From(string[]? model, string[]? variant, string[]? colour, string[]? interior, string[]? rotation)
        => new(model ?? [], variant ?? [], colour ?? [], interior ?? [], rotation ?? []);
}

public sealed record ConfigRow(
    string Model,
    string Variant,
    string Colour,
    string Interior,
    double TotalUnits,
    double RecentVelocity,
    double DecayPct,
    string TrendDirection,
    string RotationClass,
    int CurrentStock,
    double TopRegionShare,
    bool DecayAlert,
    bool StockoutSuspected,
    bool IsColdStart);

public sealed record ConfigMeta(int TotalConfigurations, int FilteredConfigurations, DateTimeOffset GeneratedAtUtc);

public sealed record ConfigSummaryResponse(ConfigDemandSummary Summary, ConfigMeta Meta);

public sealed record ConfigListResponse(IReadOnlyList<ConfigRow> Items, int Page, int PageSize, long TotalCount, ConfigMeta Meta);

public sealed record ConfigFilterOptions(
    IReadOnlyList<string> Models,
    IReadOnlyList<string> Variants,
    IReadOnlyList<string> Colours,
    IReadOnlyList<string> Interiors,
    IReadOnlyList<string> Rotations);

public static class ConfigMapping
{
    public static ConfigRow ToRow(this ConfigDemandResult c) => new(
        c.Model, c.Variant, c.Colour, c.Interior, c.TotalUnits, c.RecentVelocity, c.DecayPct, c.TrendDirection,
        c.RotationClass, c.CurrentStock, c.TopRegionShare, c.DecayAlert, c.StockoutSuspected, c.IsColdStart);

    public static IReadOnlyList<ConfigDemandResult> Sort(this IEnumerable<ConfigDemandResult> items, string? sort) =>
        (sort?.ToLowerInvariant()) switch
        {
            "velocity" => items.OrderByDescending(c => c.RecentVelocity).ThenBy(c => c.Model, StringComparer.Ordinal).ToList(),
            "decay" => items.OrderBy(c => c.DecayPct).ThenBy(c => c.Model, StringComparer.Ordinal).ToList(),
            "stock" => items.OrderByDescending(c => c.CurrentStock).ThenBy(c => c.Model, StringComparer.Ordinal).ToList(),
            _ => items.OrderByDescending(c => c.TotalUnits).ThenBy(c => c.Model, StringComparer.Ordinal).ToList(),
        };
}
