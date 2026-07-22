namespace BeeEye.Analytics.AfterSales;

/// <summary>
/// After-sales service classes, kept strictly separate throughout UC6 because planned
/// (Routine) and unplanned (Repair / Warranty / Recall) demand plan differently.
/// </summary>
public enum ServiceType
{
    Routine,
    Repair,
    Warranty,
    Recall,
}

/// <summary>One after-sales service event — the analytical grain for UC6 (pure input).</summary>
public sealed record ServiceRecord(
    string Model,
    string Variant,
    string Location,
    string ServiceMonthKey,
    int MonthsSinceSale,
    string MileageBand,
    ServiceType ServiceType,
    double LaborHours);

/// <summary>A monthly volume observation for a model (vehicle sales), used for the sales↔service correlation.</summary>
public sealed record MonthlyVolume(string Model, string MonthKey, double Units);

/// <summary>Configurable UC6 thresholds — documented assumptions, not production-validated constants.</summary>
public sealed record ServiceIntensitySettings
{
    /// <summary>Intensity-index level (fleet mean = 1.0) at or above which a model is flagged high-service.</summary>
    public double HighIntensityThreshold { get; init; } = 1.25;

    /// <summary>Maximum lag (months) explored when correlating monthly sales with monthly service volume.</summary>
    public int MaxCorrelationLagMonths { get; init; } = 12;

    public double HighCoverageRate { get; init; } = 0.80;
    public double MediumCoverageRate { get; init; } = 0.50;
    public int HighHistoryMonths { get; init; } = 12;
    public int MediumHistoryMonths { get; init; } = 6;

    public static ServiceIntensitySettings Default => new();
}

/// <summary>Event count (and per-vehicle rate where a fleet exists) for one band.</summary>
public sealed record BandCount(string Band, int Events, double? EventsPerVehicle);

/// <summary>Event count and share for one service type — all four types are always present (explicit zeros).</summary>
public sealed record ServiceTypeCount(string ServiceType, int Events, double Share);

/// <summary>Data-coverage / reliability disclosure carried on every UC6 model view.</summary>
public sealed record ServiceCoverage(
    int VehiclesInOperation,
    int VehiclesWithEvents,
    double? CoverageRate,
    int MonthsOfHistory,
    string ReliabilityTier);

/// <summary>Lagged sales→service association (never causation). Nulls when it cannot be computed reliably.</summary>
public sealed record ServiceCorrelation(
    double? Lag0,
    double? Best,
    int BestLagMonths,
    string Interpretation);

/// <summary>Per-model service-intensity result with all UC6 breakdowns.</summary>
public sealed record ModelServiceIntensity(
    string Model,
    int TotalEvents,
    int VehiclesInOperation,
    double? EventsPerVehicle,
    double? IntensityIndex,
    bool HighIntensity,
    double TotalLaborHours,
    double? LaborHoursPerVehicle,
    IReadOnlyList<BandCount> ByMileageBand,
    IReadOnlyList<BandCount> ByTimeSinceSale,
    IReadOnlyList<ServiceTypeCount> ByServiceType,
    ServiceCoverage Coverage,
    ServiceCorrelation Correlation);

/// <summary>Fleet-level UC6 summary.</summary>
public sealed record ServiceIntensitySummary(
    int ModelsTracked,
    int TotalEvents,
    int TotalVehiclesInOperation,
    double? FleetEventsPerVehicle,
    double? AverageIntensityIndex,
    int HighIntensityModels,
    double? OverallCoverageRate,
    int MonthsOfHistory);

/// <summary>The full UC6 analysis: per-model intensity plus the fleet summary.</summary>
public sealed record ServiceIntensityAnalysis(
    IReadOnlyList<ModelServiceIntensity> Models,
    ServiceIntensitySummary Summary);
