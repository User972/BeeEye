using BeeEye.Analytics.Forecasting;

namespace BeeEye.Modules.Forecasting.Contracts;

/// <summary>Server-side filter for UC2 queries.</summary>
public sealed record SalesFilter(
    IReadOnlyList<string> Brand,
    IReadOnlyList<string> Model,
    IReadOnlyList<string> Variant,
    IReadOnlyList<string> Type,
    IReadOnlyList<string> Location,
    IReadOnlyList<string> Colour,
    IReadOnlyList<string> Interior,
    string? DateFrom,
    string? DateTo)
{
    public static SalesFilter From(
        string[]? brand, string[]? model, string[]? variant, string[]? type,
        string[]? location, string[]? colour, string[]? interior, string? dateFrom, string? dateTo)
        => new(brand ?? [], model ?? [], variant ?? [], type ?? [], location ?? [], colour ?? [], interior ?? [], dateFrom, dateTo);
}

public sealed record ForecastMeta(int MonthsCovered, double HistoricalUnits, DateTimeOffset GeneratedAtUtc);

public sealed record ForecastResponse(ForecastResult Forecast, ForecastMeta Meta);

/// <summary>Back-test accuracy for one value of a product/region dimension (UC2 core view).</summary>
public sealed record DimensionAccuracy(
    string Value,
    double? Wmape,
    double? Bias,
    double Mae,
    int Units,
    string ChosenModel,
    string Tendency); // "over-forecasting" | "under-forecasting" | "balanced" | "insufficient"

public sealed record AccuracyByResponse(string Dimension, IReadOnlyList<DimensionAccuracy> Rows, ForecastMeta Meta);

public sealed record ForecastFilterOptions(
    IReadOnlyList<string> Brands,
    IReadOnlyList<string> Models,
    IReadOnlyList<string> Variants,
    IReadOnlyList<string> Types,
    IReadOnlyList<string> Locations,
    IReadOnlyList<string> Colours,
    IReadOnlyList<string> Interiors,
    string FirstMonth,
    string LastMonth);
