namespace BeeEye.Analytics.SpareParts;

/// <summary>Syntetos–Boylan–Croston (SBC) demand class for a parts series.</summary>
public enum DemandClass
{
    /// <summary>Regular, low variability — reuse the smooth/seasonal forecasting family.</summary>
    Smooth,

    /// <summary>Frequent but variable size — SBA.</summary>
    Erratic,

    /// <summary>Infrequent, stable size — Croston / SBA.</summary>
    Intermittent,

    /// <summary>Infrequent and variable — SBA / TSB, widest ranges.</summary>
    Lumpy,

    /// <summary>Declining demand probability (end-of-life / supersession) — TSB.</summary>
    Obsolescent,

    /// <summary>Too little history to classify or forecast — flagged, never fabricated.</summary>
    InsufficientData,
}

/// <summary>Per-period rate from each candidate method, for the transparent method-comparison view.</summary>
public sealed record MethodComparison(double Ses, double Croston, double Sba, double Tsb);

/// <summary>Configurable UC7 parameters — documented assumptions, editable and recompute-live.</summary>
public sealed record SparePartsSettings
{
    /// <summary>Target service level for safety stock / lead-time demand.</summary>
    public double ServiceLevel { get; init; } = 0.95;

    /// <summary>Review period (months) added to lead time for the protection interval.</summary>
    public double ReviewPeriodMonths { get; init; } = 1.0;

    /// <summary>Smoothing constant for Croston / SBA.</summary>
    public double Alpha { get; init; } = 0.1;

    /// <summary>TSB demand-probability smoothing constant.</summary>
    public double TsbAlphaProbability { get; init; } = 0.1;

    /// <summary>TSB demand-size smoothing constant.</summary>
    public double TsbAlphaSize { get; init; } = 0.1;

    /// <summary>SBC ADI threshold separating smooth/erratic from intermittent/lumpy.</summary>
    public double AdiThreshold { get; init; } = 1.32;

    /// <summary>SBC CV² threshold separating low from high size variability.</summary>
    public double Cv2Threshold { get; init; } = 0.49;

    /// <summary>Minimum non-zero observations before a part can be forecast (else insufficient-data).</summary>
    public int MinNonZeroPeriods { get; init; } = 2;

    /// <summary>Minimum months of history before a part can be forecast.</summary>
    public int MinMonths { get; init; } = 6;

    /// <summary>Second-half/first-half demand-rate ratio below which a part is judged obsolescent.</summary>
    public double ObsolescenceRatio { get; init; } = 0.5;

    public static SparePartsSettings Default => new();
}

/// <summary>Classification + forecast for one parts demand series (no fabrication when insufficient).</summary>
public sealed record IntermittentForecast(
    DemandClass Class,
    string Method,
    double Adi,
    double Cv2,
    int NonZeroPeriods,
    int Periods,
    double RatePerPeriod,
    double RangeLow,
    double RangeHigh,
    string Confidence,
    bool InsufficientData,
    MethodComparison Comparison);

/// <summary>Part master + supply position — the non-demand inputs to a stocking recommendation.</summary>
public sealed record SparePartInput(
    string PartNumber,
    string Name,
    string Category,
    int LeadTimeDays,
    int CurrentStock,
    int InboundStock,
    decimal UnitCost);

/// <summary>
/// A full UC7 stocking recommendation. Numeric stocking targets are <c>null</c> when the part is
/// insufficient-data — decision-support integrity: no fabricated numbers, only a data-collection action.
/// </summary>
public sealed record SparePartRecommendation(
    string PartNumber,
    string Name,
    string Category,
    DemandClass Class,
    string Method,
    double Adi,
    double Cv2,
    int NonZeroPeriods,
    int Periods,
    double? PredictedMonthlyDemand,
    double? MonthlyRangeLow,
    double? MonthlyRangeHigh,
    double LeadTimeMonths,
    double? LeadTimeDemand,
    double? SafetyStock,
    double? ReorderPoint,
    double? OrderUpToLevel,
    int Available,
    int? RecommendedQuantity,
    int? StockingRangeLow,
    int? StockingRangeHigh,
    string StockoutRisk,
    string HoldingRisk,
    string Confidence,
    bool InsufficientData,
    string Action,
    string Rationale,
    MethodComparison Comparison);
