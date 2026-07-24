namespace BeeEye.Analytics.Configuration;

/// <summary>Configurable thresholds for configuration-level demand classification (UC3).</summary>
public sealed record ConfigDemandSettings
{
    public int TrailingMonths { get; init; } = 3;

    /// <summary>Monthly velocity (units/month) at or above which a config is "fast-moving".</summary>
    public double FastThreshold { get; init; } = 3.0;

    /// <summary>Monthly velocity at or above which a config is "medium".</summary>
    public double MediumThreshold { get; init; } = 1.0;

    /// <summary>Recent-vs-prior decay (percent) below which a decay alert is raised.</summary>
    public double DecayAlertPct { get; init; } = -25.0;

    /// <summary>Months of sales history below which a config is flagged cold-start.</summary>
    public int ColdStartMinMonths { get; init; } = 3;

    public static ConfigDemandSettings Default => new();
}

public sealed record RegionDemand(string Region, double Units, double Share);

/// <summary>Per-configuration demand insight (UC3).</summary>
public sealed record ConfigDemandResult(
    string Model,
    string Variant,
    string Colour,
    string Interior,
    double TotalUnits,
    int MonthsWithSales,
    string FirstMonth,
    string LastMonth,
    double RecentVelocity,
    double PriorVelocity,
    double DecayPct,
    string TrendDirection,
    string RotationClass,
    bool DecayAlert,
    bool IsColdStart,
    bool StockoutSuspected,
    int CurrentStock,
    double TopRegionShare,
    IReadOnlyList<RegionDemand> ByRegion);

public sealed record RotationBand(string Key, int Configurations, double Units);

public sealed record ConfigDemandSummary(
    int Configurations,
    double TotalUnits,
    int FastCount,
    int MediumCount,
    int SlowCount,
    int DeadCount,
    int DecayAlerts,
    int ColdStart,
    int StockoutSuspected,
    IReadOnlyList<RotationBand> ByRotation);
