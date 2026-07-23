using BeeEye.Analytics.AfterSales;
using BeeEye.Shared.Paging;

namespace BeeEye.Modules.AfterSales.Contracts;

/// <summary>
/// Data-provenance disclosure carried on every UC6 response. After-sales data is
/// <b>synthetic demo data</b> derived deterministically from the real sales history — it is not real
/// and not Oracle Fusion.
/// </summary>
public sealed record AfterSalesProvenance(string Provenance, string Note, DateTimeOffset GeneratedAtUtc)
{
    public const string SyntheticDemo = "synthetic-demo";

    public static AfterSalesProvenance Now() => new(
        SyntheticDemo,
        "Synthetic demo data derived deterministically from real vehicle sales — not real after-sales data and not Oracle Fusion.",
        DateTimeOffset.UtcNow);
}

/// <summary>Fleet-level UC6 summary with provenance.</summary>
public sealed record ServiceIntensitySummaryResponse(ServiceIntensitySummary Summary, AfterSalesProvenance Meta);

/// <summary>A compact per-model row for the sortable/paged by-model list.</summary>
public sealed record ModelIntensityRow(
    string Model,
    int TotalEvents,
    int VehiclesInOperation,
    double? EventsPerVehicle,
    double? IntensityIndex,
    bool HighIntensity,
    double TotalLaborHours,
    double? LaborHoursPerVehicle,
    double? CoverageRate,
    int MonthsOfHistory,
    string ReliabilityTier)
{
    public static ModelIntensityRow From(ModelServiceIntensity m) => new(
        m.Model, m.TotalEvents, m.VehiclesInOperation, m.EventsPerVehicle, m.IntensityIndex, m.HighIntensity,
        m.TotalLaborHours, m.LaborHoursPerVehicle, m.Coverage.CoverageRate, m.Coverage.MonthsOfHistory,
        m.Coverage.ReliabilityTier);
}

public sealed record ByModelResponse(PagedResult<ModelIntensityRow> Page, AfterSalesProvenance Meta);

/// <summary>Full per-model detail: all breakdowns, coverage and the sales↔service association.</summary>
public sealed record ModelDetailResponse(ModelServiceIntensity Model, AfterSalesProvenance Meta);

public sealed record AfterSalesFilterOptions(
    IReadOnlyList<string> Models,
    IReadOnlyList<string> Variants,
    IReadOnlyList<string> Locations,
    IReadOnlyList<string> MileageBands,
    IReadOnlyList<string> ServiceTypes);
