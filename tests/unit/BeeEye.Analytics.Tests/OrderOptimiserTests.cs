using BeeEye.Analytics.Optimisation;
using Xunit;

namespace BeeEye.Analytics.Tests;

/// <summary>
/// Tests for <see cref="OrderOptimiser.Recommend"/>. Every expected value is computed by
/// hand from the source formulas:
///   safetyStock  = max(0, monthlyVelocity * TargetCoverMonths)
///   target       = forecastDemand + safetyStock
///   available    = CurrentInventory + InboundInventory + ConfirmedOrders
///   net          = max(0, round(target, AwayFromZero) - available)
///   recommended  = lot-sizing(net): max(net, MinOrderQuantity)
///                  then ceil to OrderMultiple (when &gt; 1) then cap at max(0, AllocationLimit)
///   overstock    = available &gt; target*1.5 ? High : available &gt; target*1.2 ? Medium : Low
///   understock   = recommended &lt; net ? High : available &lt; forecastDemand ? Medium : Low
/// The rationale uses a U+2014 EM DASH ("—") in the no-order branch.
/// </summary>
public sealed class OrderOptimiserTests
{
    private static OrderConstraints Constraints(
        int current = 0,
        int inbound = 0,
        int confirmed = 0,
        int minOrderQuantity = 0,
        int orderMultiple = 1,
        int? allocationLimit = null,
        double targetCoverMonths = 1.0) =>
        new()
        {
            CurrentInventory = current,
            InboundInventory = inbound,
            ConfirmedOrders = confirmed,
            MinOrderQuantity = minOrderQuantity,
            OrderMultiple = orderMultiple,
            AllocationLimit = allocationLimit,
            TargetCoverMonths = targetCoverMonths,
        };

    // Scenario A — the task's worked example.
    // forecast=30, velocity=10, cover=1 => safety=10, target=40, available=17, net=23,
    // MOQ=6 (no bump), multiple=5 => 23->25, no allocation cap.
    [Fact]
    public void Recommend_WorkedExample_AppliesMultipleAndPassesConfidenceThrough()
    {
        var r = OrderOptimiser.Recommend(
            forecastDemand: 30,
            monthlyVelocity: 10,
            constraints: Constraints(current: 5, inbound: 10, confirmed: 2, minOrderQuantity: 6, orderMultiple: 5));

        Assert.Equal(30, r.ForecastDemand, 6);
        Assert.Equal(10, r.SafetyStock, 6);
        Assert.Equal(17, r.Available);
        Assert.Equal(23, r.NetRequirement);
        Assert.Equal(25, r.RecommendedQuantity);
        Assert.Equal("Low", r.OverstockRisk);
        Assert.Equal("Medium", r.UnderstockRisk); // available 17 < forecast 30
        Assert.Equal("Medium", r.Confidence);     // default pass-through
        Assert.Equal(
            "Forecast 30 + 10 safety, less 17 available, gives a net need of 23; lot-sizing recommends 25.",
            r.Rationale);
    }

    // Scenario B — available covers target; no order; overstock Medium; cover format "1.5".
    // forecast=10, velocity=4, cover=1.5 => safety=6, target=16, available=20, net=0.
    // overstock: 20>24? no; 20>19.2? yes => Medium.
    [Fact]
    public void Recommend_AvailableCoversTarget_NoOrder_MediumOverstock()
    {
        var r = OrderOptimiser.Recommend(
            forecastDemand: 10,
            monthlyVelocity: 4,
            constraints: Constraints(current: 20, targetCoverMonths: 1.5));

        Assert.Equal(6, r.SafetyStock, 6);
        Assert.Equal(20, r.Available);
        Assert.Equal(0, r.NetRequirement);
        Assert.Equal(0, r.RecommendedQuantity);
        Assert.Equal("Medium", r.OverstockRisk);
        Assert.Equal("Low", r.UnderstockRisk);
        Assert.Equal(
            "Available supply (20) already covers forecast demand plus 1.5 months safety — no order required.",
            r.Rationale);
    }

    // Scenario C — no order; overstock High (available > target*1.5).
    // forecast=10, velocity=0 => safety=0, target=10, available=20 (>15) => High.
    [Fact]
    public void Recommend_AvailableFarAboveTarget_NoOrder_HighOverstock()
    {
        var r = OrderOptimiser.Recommend(
            forecastDemand: 10,
            monthlyVelocity: 0,
            constraints: Constraints(current: 20));

        Assert.Equal(0, r.SafetyStock, 6);
        Assert.Equal(20, r.Available);
        Assert.Equal(0, r.NetRequirement);
        Assert.Equal(0, r.RecommendedQuantity);
        Assert.Equal("High", r.OverstockRisk);
        Assert.Equal("Low", r.UnderstockRisk);
        Assert.Equal(
            "Available supply (20) already covers forecast demand plus 1 months safety — no order required.",
            r.Rationale);
    }

    // Scenario D — allocation-limited: recommended < net => understock High; confidence pass-through.
    // forecast=30, velocity=10 => safety=10, target=40, available=0, net=40; allocation caps to 20.
    [Fact]
    public void Recommend_AllocationLimited_HighUnderstock_ConfidencePassesThrough()
    {
        var r = OrderOptimiser.Recommend(
            forecastDemand: 30,
            monthlyVelocity: 10,
            constraints: Constraints(minOrderQuantity: 1, allocationLimit: 20),
            demandConfidence: "High");

        Assert.Equal(10, r.SafetyStock, 6);
        Assert.Equal(0, r.Available);
        Assert.Equal(40, r.NetRequirement);
        Assert.Equal(20, r.RecommendedQuantity);
        Assert.Equal("Low", r.OverstockRisk);
        Assert.Equal("High", r.UnderstockRisk);
        Assert.Equal("High", r.Confidence);
        Assert.Equal(
            "Forecast 30 + 10 safety, less 0 available, gives a net need of 40; lot-sizing recommends 20.",
            r.Rationale);
    }

    // Scenario E — MinOrderQuantity bumps the net up; understock Medium (available < forecast).
    // forecast=5, velocity=0 => safety=0, target=5, available=2, net=3, MOQ=6 => 6.
    [Fact]
    public void Recommend_MinOrderQuantityBumpsUp_MediumUnderstock()
    {
        var r = OrderOptimiser.Recommend(
            forecastDemand: 5,
            monthlyVelocity: 0,
            constraints: Constraints(current: 2, minOrderQuantity: 6));

        Assert.Equal(0, r.SafetyStock, 6);
        Assert.Equal(2, r.Available);
        Assert.Equal(3, r.NetRequirement);
        Assert.Equal(6, r.RecommendedQuantity);
        Assert.Equal("Low", r.OverstockRisk);
        Assert.Equal("Medium", r.UnderstockRisk);
        Assert.Equal(
            "Forecast 5 + 0 safety, less 2 available, gives a net need of 3; lot-sizing recommends 6.",
            r.Rationale);
    }

    // Scenario F — safety-stock/target rounding uses MidpointRounding.AwayFromZero.
    // forecast=10, velocity=5, cover=0.5 => safety=2.5, target=12.5 -> round 13.
    [Fact]
    public void Recommend_TargetHalf_RoundsAwayFromZero()
    {
        var r = OrderOptimiser.Recommend(
            forecastDemand: 10,
            monthlyVelocity: 5,
            constraints: Constraints(minOrderQuantity: 1, targetCoverMonths: 0.5));

        Assert.Equal(2.5, r.SafetyStock, 6);
        Assert.Equal(0, r.Available);
        Assert.Equal(13, r.NetRequirement); // 12.5 rounds to 13, not 12
        Assert.Equal(13, r.RecommendedQuantity);
        Assert.Equal("Low", r.OverstockRisk);
        Assert.Equal("Medium", r.UnderstockRisk);
    }

    // Scenario G — negative velocity clamps safety stock to 0 via Math.Max(0, ...).
    // forecast=10, velocity=-5 => safety=0, target=10, available=0, net=10.
    [Fact]
    public void Recommend_NegativeVelocity_ClampsSafetyStockToZero()
    {
        var r = OrderOptimiser.Recommend(
            forecastDemand: 10,
            monthlyVelocity: -5,
            constraints: Constraints(minOrderQuantity: 1));

        Assert.Equal(0, r.SafetyStock, 6);
        Assert.Equal(0, r.Available);
        Assert.Equal(10, r.NetRequirement);
        Assert.Equal(10, r.RecommendedQuantity);
        Assert.Equal("Low", r.OverstockRisk);
        Assert.Equal("Medium", r.UnderstockRisk);
        Assert.Equal(
            "Forecast 10 + 0 safety, less 0 available, gives a net need of 10; lot-sizing recommends 10.",
            r.Rationale);
    }

    // Scenario H — negative AllocationLimit is floored to 0 => recommended 0 => understock High.
    // forecast=20, velocity=0 => target=20, available=0, net=20; allocation=-5 => cap 0.
    [Fact]
    public void Recommend_NegativeAllocationLimit_FlooredToZero_HighUnderstock()
    {
        var r = OrderOptimiser.Recommend(
            forecastDemand: 20,
            monthlyVelocity: 0,
            constraints: Constraints(minOrderQuantity: 1, allocationLimit: -5));

        Assert.Equal(20, r.NetRequirement);
        Assert.Equal(0, r.RecommendedQuantity);
        Assert.Equal("Low", r.OverstockRisk);
        Assert.Equal("High", r.UnderstockRisk);
        Assert.Equal(
            "Forecast 20 + 0 safety, less 0 available, gives a net need of 20; lot-sizing recommends 0.",
            r.Rationale);
    }

    // Scenario I — quantity already a multiple (no ceil change); allocation above qty does not cap.
    // forecast=25, velocity=0 => target=25, available=0, net=25; multiple=5 => 25; allocation=100.
    [Fact]
    public void Recommend_QuantityAlreadyMultiple_AllocationDoesNotCap()
    {
        var r = OrderOptimiser.Recommend(
            forecastDemand: 25,
            monthlyVelocity: 0,
            constraints: Constraints(minOrderQuantity: 1, orderMultiple: 5, allocationLimit: 100));

        Assert.Equal(25, r.NetRequirement);
        Assert.Equal(25, r.RecommendedQuantity);
        Assert.Equal("Low", r.OverstockRisk);
        Assert.Equal("Medium", r.UnderstockRisk); // available 0 < forecast 25
        Assert.Equal(
            "Forecast 25 + 0 safety, less 0 available, gives a net need of 25; lot-sizing recommends 25.",
            r.Rationale);
    }

    // Scenario J — an order is needed but available >= forecast => understock Low branch.
    // forecast=10, velocity=5, cover=1 => safety=5, target=15, available=12, net=3, recommended=3.
    [Fact]
    public void Recommend_OrderNeededButAvailableCoversForecast_LowUnderstock()
    {
        var r = OrderOptimiser.Recommend(
            forecastDemand: 10,
            monthlyVelocity: 5,
            constraints: Constraints(current: 12, minOrderQuantity: 1));

        Assert.Equal(5, r.SafetyStock, 6);
        Assert.Equal(12, r.Available);
        Assert.Equal(3, r.NetRequirement);
        Assert.Equal(3, r.RecommendedQuantity);
        Assert.Equal("Low", r.OverstockRisk);
        Assert.Equal("Low", r.UnderstockRisk);
        Assert.Equal(
            "Forecast 10 + 5 safety, less 12 available, gives a net need of 3; lot-sizing recommends 3.",
            r.Rationale);
    }

    // Available combines all three supply sources (current + inbound + confirmed).
    [Fact]
    public void Recommend_AvailableSumsCurrentInboundConfirmed()
    {
        var r = OrderOptimiser.Recommend(
            forecastDemand: 100,
            monthlyVelocity: 0,
            constraints: Constraints(current: 3, inbound: 4, confirmed: 5, minOrderQuantity: 1));

        Assert.Equal(12, r.Available);            // 3 + 4 + 5
        Assert.Equal(88, r.NetRequirement);       // 100 - 12
        Assert.Equal(88, r.RecommendedQuantity);
    }
}
