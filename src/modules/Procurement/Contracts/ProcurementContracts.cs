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
