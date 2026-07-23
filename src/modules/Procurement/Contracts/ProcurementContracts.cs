using BeeEye.Analytics.Optimisation;

namespace BeeEye.Modules.Procurement.Contracts;

/// <summary>Configurable procurement scenario. Lead time defaults to the observed per-config
/// average from inventory; other inputs are documented assumptions until PO data is integrated.</summary>
public sealed record ProcurementScenario(
    double ServiceLevel,
    double? LeadTimeMonthsOverride,
    double ReviewPeriodMonths,
    int MinOrderQuantity,
    int OrderMultiple,
    int Inbound)
{
    public static ProcurementScenario From(
        double? serviceLevel, double? leadTimeMonths, double? reviewPeriodMonths, int? minOrderQuantity, int? orderMultiple, int? inbound)
        => new(
            serviceLevel ?? 0.95,
            leadTimeMonths,
            reviewPeriodMonths ?? 1.0,
            minOrderQuantity ?? 0,
            orderMultiple ?? 1,
            inbound ?? 0);

    /// <summary>
    /// Guards the optimiser's numeric domain: query-bound doubles admit negatives and NaN
    /// ("NaN" parses as a valid double), which would drive Math.Sqrt of a negative variance
    /// to NaN and crash JSON serialisation with a 500 instead of a client error.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (!double.IsFinite(ServiceLevel) || ServiceLevel is <= 0 or >= 1)
        {
            errors.Add("serviceLevel must be between 0 and 1 (exclusive).");
        }

        // Upper bounds matter as much as sign: a huge finite input (1e308) overflows the
        // optimiser's multiplications to Infinity, which is just as unserialisable as NaN.
        if (LeadTimeMonthsOverride is { } lead && (!double.IsFinite(lead) || lead is <= 0 or > 120))
        {
            errors.Add("leadTimeMonths must be a positive number of months (at most 120).");
        }

        if (!double.IsFinite(ReviewPeriodMonths) || ReviewPeriodMonths is < 0 or > 120)
        {
            errors.Add("reviewPeriodMonths must be between 0 and 120 months.");
        }

        if (MinOrderQuantity < 0)
        {
            errors.Add("minOrderQuantity must be zero or positive.");
        }

        if (OrderMultiple < 1)
        {
            errors.Add("orderMultiple must be at least 1.");
        }

        if (Inbound < 0)
        {
            errors.Add("inbound must be zero or positive.");
        }

        return errors;
    }
}

public sealed record ProcurementRow(
    string Model,
    string Variant,
    double DemandMean,
    double DemandStd,
    double LeadTimeMonths,
    double SafetyStock,
    double ReorderPoint,
    double OrderUpToLevel,
    int Available,
    int RecommendedQuantity,
    int RangeLow,
    int RangeHigh,
    string StockoutRisk,
    string Confidence,
    string Rationale)
{
    public static ProcurementRow From(string model, string variant, double leadTimeMonths, ProcurementRecommendation r)
        => new(
            model, variant, r.DemandMean, r.DemandStd, leadTimeMonths, r.SafetyStock, r.ReorderPoint, r.OrderUpToLevel,
            r.Available, r.RecommendedQuantity, r.RangeLow, r.RangeHigh, r.StockoutRisk, r.Confidence, r.Rationale);
}

public sealed record ProcurementMeta(int Configurations, int TotalRecommendedUnits, DateTimeOffset GeneratedAtUtc);

public sealed record ProcurementResponse(ProcurementScenario Scenario, IReadOnlyList<ProcurementRow> Items, ProcurementMeta Meta);
