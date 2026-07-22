namespace BeeEye.Analytics.Optimisation;

/// <summary>
/// Business constraints for monthly vehicle order optimisation (UC1). Deliberately
/// separate from the demand forecast: the forecast is an input, the constraints and
/// this calculation turn it into an order recommendation.
/// </summary>
public sealed record OrderConstraints
{
    public int CurrentInventory { get; init; }
    public int InboundInventory { get; init; }
    public int ConfirmedOrders { get; init; }
    public int MinOrderQuantity { get; init; }
    public int OrderMultiple { get; init; } = 1;
    public int? AllocationLimit { get; init; }

    /// <summary>Desired ending safety cover, expressed in months of demand.</summary>
    public double TargetCoverMonths { get; init; } = 1.0;
}

public sealed record OrderRecommendation(
    double ForecastDemand,
    double SafetyStock,
    int Available,
    int NetRequirement,
    int RecommendedQuantity,
    string OverstockRisk,
    string UnderstockRisk,
    string Confidence,
    string Rationale);

/// <summary>
/// Turns a demand forecast plus supply constraints into a recommended order quantity,
/// never presenting an unconstrained forecast as an order. Existing and inbound supply
/// is always netted off first.
/// </summary>
public static class OrderOptimiser
{
    public static OrderRecommendation Recommend(
        double forecastDemand, double monthlyVelocity, OrderConstraints constraints, string demandConfidence = "Medium")
    {
        var safetyStock = Math.Max(0, monthlyVelocity * constraints.TargetCoverMonths);
        var target = forecastDemand + safetyStock;
        var available = constraints.CurrentInventory + constraints.InboundInventory + constraints.ConfirmedOrders;

        var netRequirement = Math.Max(0, (int)Math.Round(target, MidpointRounding.AwayFromZero) - available);
        var recommended = ApplyLotSizing(netRequirement, constraints);

        var overstock = available > target * 1.5 ? "High" : available > target * 1.2 ? "Medium" : "Low";
        var understock = recommended < netRequirement ? "High"
            : available < forecastDemand ? "Medium"
            : "Low";

        var rationale = netRequirement == 0
            ? $"Available supply ({available}) already covers forecast demand plus {constraints.TargetCoverMonths:0.#} months safety — no order required."
            : $"Forecast {forecastDemand:0} + {safetyStock:0} safety, less {available} available, gives a net need of {netRequirement}; lot-sizing recommends {recommended}.";

        return new OrderRecommendation(
            forecastDemand, safetyStock, available, netRequirement, recommended, overstock, understock, demandConfidence, rationale);
    }

    private static int ApplyLotSizing(int netRequirement, OrderConstraints c)
    {
        if (netRequirement <= 0)
        {
            return 0;
        }

        var qty = Math.Max(netRequirement, c.MinOrderQuantity);
        if (c.OrderMultiple > 1)
        {
            qty = (int)Math.Ceiling(qty / (double)c.OrderMultiple) * c.OrderMultiple;
        }

        if (c.AllocationLimit is { } limit)
        {
            qty = Math.Min(qty, Math.Max(0, limit));
        }

        return qty;
    }
}
