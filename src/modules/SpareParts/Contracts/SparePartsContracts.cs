using BeeEye.Analytics.SpareParts;
using BeeEye.Shared.Paging;

namespace BeeEye.Modules.SpareParts.Contracts;

/// <summary>Configurable UC7 scenario (service level + review period) — nullable query params with in-code defaults.</summary>
public sealed record SparePartsScenario(double ServiceLevel, double ReviewPeriodMonths)
{
    public static SparePartsScenario From(double? serviceLevel, double? reviewPeriodMonths)
        => new(serviceLevel ?? 0.95, reviewPeriodMonths ?? 1.0);

    /// <summary>
    /// Guards the forecaster's numeric domain: query-bound doubles admit negatives, NaN
    /// ("NaN" parses as a valid double) and huge finite values, which would drive
    /// <c>Math.Sqrt</c> of a negative protection interval to NaN (or overflow to Infinity)
    /// and crash JSON serialisation with a 500 instead of a client error. Mirrors
    /// <c>ProcurementScenario.Validate()</c> / <c>OrderScenario.Validate()</c>.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (!double.IsFinite(ServiceLevel) || ServiceLevel is <= 0 or >= 1)
        {
            errors.Add("serviceLevel must be between 0 and 1 (exclusive).");
        }

        if (!double.IsFinite(ReviewPeriodMonths) || ReviewPeriodMonths is < 0 or > 120)
        {
            errors.Add("reviewPeriodMonths must be between 0 and 120 months.");
        }

        return errors;
    }

    public SparePartsSettings ToSettings() => new()
    {
        ServiceLevel = ServiceLevel,
        ReviewPeriodMonths = ReviewPeriodMonths,
    };
}

/// <summary>Data-provenance disclosure carried on every UC7 response (synthetic demo data, not Oracle Fusion).</summary>
public sealed record SparePartsProvenance(string Provenance, string Note, DateTimeOffset GeneratedAtUtc)
{
    public const string SyntheticDemo = "synthetic-demo";

    public static SparePartsProvenance Now() => new(
        SyntheticDemo,
        "Synthetic demo data derived deterministically from real vehicle sales — not real parts/service data and not Oracle Fusion.",
        DateTimeOffset.UtcNow);
}

/// <summary>
/// One row in the parts demand table — the stocking grain is <b>part × location</b> (each workshop
/// stocks and reorders independently), which is where spare-parts demand is genuinely intermittent.
/// </summary>
public sealed record PartDemandRow(
    string PartNumber,
    string Name,
    string Category,
    string Location,
    string DemandClass,
    string Method,
    double? PredictedMonthlyDemand,
    int? StockingRangeLow,
    int? StockingRangeHigh,
    int CurrentStock,
    int InboundStock,
    int LeadTimeDays,
    double? ReorderPoint,
    string StockoutRisk,
    string HoldingRisk,
    string Confidence,
    bool InsufficientData);

public sealed record DemandClassCount(string DemandClass, int Count);

public sealed record SparePartsSummary(
    int DistinctParts,
    int StockingPoints,
    int LowDataPoints,
    int AtRiskPoints,
    double PredictedMonthlyDemandTotal,
    IReadOnlyList<DemandClassCount> ByDemandClass);

public sealed record SparePartsSummaryResponse(SparePartsScenario Scenario, SparePartsSummary Summary, SparePartsProvenance Meta);

public sealed record PartsDemandResponse(
    SparePartsScenario Scenario, PagedResult<PartDemandRow> Page, SparePartsProvenance Meta);

/// <summary>A month + quantity in a part's usage history (dense — zero months are real signal).</summary>
public sealed record UsagePoint(string Month, double Quantity);

public sealed record SupersessionInfo(string OldPartNumber, string NewPartNumber, DateOnly EffectiveDate);

/// <summary>
/// Single-part deep dive: the national rolled-up recommendation (headline + Croston/SBA/TSB comparison +
/// forecast range), the dense national usage history, and the per-location stocking rows.
/// </summary>
public sealed record PartDetailResponse(
    SparePartsScenario Scenario,
    SparePartRecommendation National,
    IReadOnlyList<UsagePoint> UsageHistory,
    IReadOnlyList<PartDemandRow> ByLocation,
    IReadOnlyList<string> CompatibleModels,
    IReadOnlyList<SupersessionInfo> RolledUpSupersessions,
    string? SupersededByPartNumber,
    SparePartsProvenance Meta);

public sealed record SparePartsFilterOptions(
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Models,
    IReadOnlyList<string> DemandClasses);
