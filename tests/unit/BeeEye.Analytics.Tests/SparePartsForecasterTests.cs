using System;
using System.Collections.Generic;
using BeeEye.Analytics.SpareParts;
using Xunit;

namespace BeeEye.Analytics.Tests;

/// <summary>Deterministic tests for the UC7 stocking recommender.</summary>
public class SparePartsForecasterTests
{
    private static SparePartInput Part(int lead = 30, int current = 0, int inbound = 0)
        => new("OF-1001", "Oil filter", "Filters", lead, current, inbound, 25.00m);

    // A clean intermittent series with genuine variability (so safety stock is non-zero).
    private static readonly double[] Intermittent =
        [0, 0, 5, 0, 0, 5, 0, 0, 5, 0, 0, 5, 0, 0, 5, 0, 0, 5];

    [Fact]
    public void Recommend_ProducesAStockingRange()
    {
        var r = SparePartsForecaster.Recommend(Part(), Intermittent, SparePartsSettings.Default);
        Assert.False(r.InsufficientData);
        Assert.NotNull(r.RecommendedQuantity);
        Assert.NotNull(r.StockingRangeLow);
        Assert.NotNull(r.StockingRangeHigh);
        Assert.True(r.StockingRangeHigh >= r.StockingRangeLow);
        // Point estimate (service level 0.95) sits within the 0.90–0.99 range.
        Assert.InRange(r.RecommendedQuantity!.Value, r.StockingRangeLow!.Value, r.StockingRangeHigh!.Value);
        Assert.Equal(DemandClass.Intermittent, r.Class);
        Assert.True(r.LeadTimeMonths > 0);
    }

    [Fact]
    public void Recommend_HigherServiceLevel_NeverReducesSafetyStockOrRange()
    {
        var low = SparePartsForecaster.Recommend(Part(), Intermittent, new SparePartsSettings { ServiceLevel = 0.90 });
        var high = SparePartsForecaster.Recommend(Part(), Intermittent, new SparePartsSettings { ServiceLevel = 0.99 });

        Assert.True(high.SafetyStock! > low.SafetyStock!);
        Assert.True(high.ReorderPoint! >= low.ReorderPoint!);
        Assert.True(high.OrderUpToLevel! >= low.OrderUpToLevel!);
        Assert.True(high.RecommendedQuantity! >= low.RecommendedQuantity!);
    }

    [Fact]
    public void Recommend_InsufficientData_YieldsNoFabricatedTargets()
    {
        // 6 months, a single demand -> insufficient.
        var r = SparePartsForecaster.Recommend(Part(), [0, 0, 0, 5, 0, 0], SparePartsSettings.Default);
        Assert.True(r.InsufficientData);
        Assert.Null(r.RecommendedQuantity);
        Assert.Null(r.StockingRangeLow);
        Assert.Null(r.StockingRangeHigh);
        Assert.Null(r.SafetyStock);
        Assert.Null(r.ReorderPoint);
        Assert.Null(r.PredictedMonthlyDemand);
        Assert.Contains("Investigate", r.Action, StringComparison.Ordinal);
    }

    [Fact]
    public void Recommend_AmpleStock_OrdersNothingAndFlagsOverstock()
    {
        var r = SparePartsForecaster.Recommend(Part(current: 500, inbound: 0), Intermittent, SparePartsSettings.Default);
        Assert.Equal(0, r.RecommendedQuantity);
        Assert.Equal("Low", r.StockoutRisk);
        Assert.Equal("Overstock", r.HoldingRisk);
        Assert.Equal("Reduce / trim stocking range", r.Action);
    }

    [Fact]
    public void Recommend_NoStock_FlagsStockoutRisk()
    {
        var r = SparePartsForecaster.Recommend(Part(lead: 90, current: 0, inbound: 0), Intermittent, SparePartsSettings.Default);
        Assert.Equal("High", r.StockoutRisk);
        Assert.Equal("Raise stocking level", r.Action);
    }

    [Fact]
    public void Recommend_NetsOffInboundSupply()
    {
        var withInbound = SparePartsForecaster.Recommend(Part(current: 0, inbound: 20), Intermittent, SparePartsSettings.Default);
        var without = SparePartsForecaster.Recommend(Part(current: 0, inbound: 0), Intermittent, SparePartsSettings.Default);
        Assert.Equal(20, withInbound.Available);
        Assert.True(withInbound.RecommendedQuantity! <= without.RecommendedQuantity!);
    }

    [Fact]
    public void Recommend_SmoothPart_UsesSesHighConfidence()
    {
        double[] smooth = [10, 11, 9, 10, 12, 10, 11, 9, 10, 11];
        var r = SparePartsForecaster.Recommend(Part(current: 0), smooth, SparePartsSettings.Default);
        Assert.Equal(DemandClass.Smooth, r.Class);
        Assert.Equal("SES", r.Method);
        Assert.Equal("High", r.Confidence);
    }

    [Fact]
    public void RollUpUsage_SumsAlignedSeries()
    {
        var rolled = SparePartsForecaster.RollUpUsage([[1, 0, 2], [0, 3, 0]]);
        Assert.Equal([1, 3, 2], rolled);
    }

    [Fact]
    public void RollUpUsage_SupersessionChain_CombinesDemand()
    {
        // Successor inherits the superseded parts' historical usage before forecasting.
        var rolled = SparePartsForecaster.RollUpUsage([[0, 0, 3, 0], [2, 0, 0, 0], [0, 1, 0, 4]]);
        Assert.Equal([2, 1, 3, 4], rolled);
    }

    [Fact]
    public void RollUpUsage_MismatchedLengths_Throws()
    {
        Assert.Throws<ArgumentException>(() => SparePartsForecaster.RollUpUsage([[1, 2, 3], [1, 2]]));
    }

    [Fact]
    public void RollUpUsage_Empty_ReturnsEmpty()
    {
        Assert.Empty(SparePartsForecaster.RollUpUsage([]));
    }
}
