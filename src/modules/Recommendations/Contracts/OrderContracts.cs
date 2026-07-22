using BeeEye.Analytics.Optimisation;

namespace BeeEye.Modules.Recommendations.Contracts;

/// <summary>A configurable ordering scenario (assumptions where source data is not yet integrated).</summary>
public sealed record OrderScenario(
    int Horizon,
    double TargetCoverMonths,
    int MinOrderQuantity,
    int OrderMultiple,
    int Inbound,
    int ConfirmedOrders,
    int? AllocationLimit)
{
    public static OrderScenario From(
        int? horizon, double? targetCoverMonths, int? minOrderQuantity, int? orderMultiple,
        int? inbound, int? confirmedOrders, int? allocationLimit)
        => new(
            horizon ?? 3,
            targetCoverMonths ?? 1.0,
            minOrderQuantity ?? 0,
            orderMultiple ?? 1,
            inbound ?? 0,
            confirmedOrders ?? 0,
            allocationLimit);
}

/// <summary>One order recommendation for a model·variant (the ordering grain).</summary>
public sealed record OrderRecommendationRow(
    string Model,
    string Variant,
    string ChosenModel,
    double? Wmape,
    double MonthlyVelocity,
    double ForecastDemand,
    double SafetyStock,
    int Available,
    int NetRequirement,
    int RecommendedQuantity,
    string OverstockRisk,
    string UnderstockRisk,
    string Confidence,
    string Rationale)
{
    public static OrderRecommendationRow From(
        string model, string variant, string chosenModel, double? wmape, double monthlyVelocity, OrderRecommendation r)
        => new(
            model, variant, chosenModel, wmape, monthlyVelocity, r.ForecastDemand, r.SafetyStock, r.Available,
            r.NetRequirement, r.RecommendedQuantity, r.OverstockRisk, r.UnderstockRisk, r.Confidence, r.Rationale);
}

public sealed record OrderMeta(int Configurations, int TotalRecommendedUnits, DateTimeOffset GeneratedAtUtc);

public sealed record OrderResponse(OrderScenario Scenario, IReadOnlyList<OrderRecommendationRow> Items, OrderMeta Meta);
