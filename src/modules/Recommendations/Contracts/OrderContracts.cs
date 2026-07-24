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

    /// <summary>
    /// Guards the optimiser's numeric domain: query-bound doubles admit negatives and NaN
    /// ("NaN" parses as a valid double), which would propagate through the safety-stock
    /// maths and crash JSON serialisation with a 500 instead of a client error.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Horizon is < 1 or > 36)
        {
            errors.Add("horizon must be between 1 and 36 months.");
        }

        // Upper bound matters as much as sign: a huge finite input (1e308) overflows the
        // safety-stock multiplication to Infinity, which is just as unserialisable as NaN.
        if (!double.IsFinite(TargetCoverMonths) || TargetCoverMonths is < 0 or > 120)
        {
            errors.Add("targetCoverMonths must be between 0 and 120 months.");
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

        if (ConfirmedOrders < 0)
        {
            errors.Add("confirmedOrders must be zero or positive.");
        }

        if (AllocationLimit is < 0)
        {
            errors.Add("allocationLimit must be zero or positive.");
        }

        return errors;
    }
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
