using BeeEye.Analytics;
using BeeEye.Analytics.Demand;
using BeeEye.Analytics.Inventory;
using Xunit;

namespace BeeEye.Analytics.Tests;

/// <summary>
/// Tests for <see cref="DiscountResponses"/> and <see cref="InventoryRiskCalculator"/>.
/// Values are hand-computed where deterministic; iterative outputs are checked via
/// invariants. The range separator produced by the source is a U+2013 EN DASH ("–").
/// </summary>
public sealed class InventoryRiskTests
{
    private static readonly DateOnly Ad = new(2026, 6, 30);

    private static RiskSettings Settings(RiskWeights? weights = null) =>
        weights is null
            ? new RiskSettings { AnalysisDate = Ad }
            : new RiskSettings { AnalysisDate = Ad, Weights = weights };

    private static InventoryUnit Unit(
        string stockId,
        string location,
        string model,
        string variant,
        DateOnly purchase,
        DateOnly? manufacture = null,
        decimal holdingCostPerDay = 100m,
        int leadTimeDays = 30,
        decimal purchasePrice = 100_000m) =>
        new(
            stockId,
            "CH-" + stockId,
            "Brand",
            model,
            variant,
            "White",
            "Black",
            "SUV",
            location,
            purchase,
            manufacture ?? purchase,
            null,
            purchasePrice,
            holdingCostPerDay,
            leadTimeDays);

    private static SalesRow Sale(
        string location,
        string model,
        string variant,
        string month,
        double units,
        bool discounted = false,
        int discountPct = 0) =>
        new(location, model, variant, month, units, discounted, discountPct);

    // ---------------------------------------------------------------------
    // DiscountResponses.Build
    // ---------------------------------------------------------------------

    [Fact]
    public void Build_Responsive_WhenDiscountedAvgAboveThresholdAndAtLeastFiveRows()
    {
        var sales = new[]
        {
            Sale("L", "A", "X", "2026-06", 10, discounted: true, discountPct: 5),
            Sale("L", "A", "X", "2026-06", 10, discounted: true, discountPct: 10),
            Sale("L", "A", "X", "2026-05", 10, discounted: true, discountPct: 10),
            Sale("L", "A", "X", "2026-05", 10, discounted: true, discountPct: 20),
            Sale("L", "A", "X", "2026-04", 10, discounted: true, discountPct: 30),
            Sale("L", "A", "X", "2026-06", 5),
            Sale("L", "A", "X", "2026-05", 5),
        };

        var result = DiscountResponses.Build(sales);
        var dr = result["A|X"];

        Assert.True(dr.Responsive);
        // distinct positive pcts sorted = [5,10,20,30]; median index 4/2 = 2 -> 20; min(15,20) = 15.
        Assert.Equal(15, dr.Suggest);
        Assert.Equal("5%–30%", dr.Range);
        Assert.Equal(10.0, dr.DiscountedAvg, 6);
        Assert.Equal(5.0, dr.NonDiscountedAvg, 6);
    }

    [Fact]
    public void Build_NotResponsive_WhenFewerThanFiveDiscountedRows()
    {
        var sales = new[]
        {
            Sale("L", "F", "R", "2026-06", 10, discounted: true, discountPct: 10),
            Sale("L", "F", "R", "2026-05", 10, discounted: true, discountPct: 10),
            Sale("L", "F", "R", "2026-04", 10, discounted: true, discountPct: 10),
            Sale("L", "F", "R", "2026-03", 10, discounted: true, discountPct: 10),
            Sale("L", "F", "R", "2026-06", 1),
        };

        var dr = DiscountResponses.Build(sales)["F|R"];

        // Avg would clear the 1.03x bar, but only 4 discounted rows.
        Assert.False(dr.Responsive);
        Assert.Equal(10.0, dr.DiscountedAvg, 6);
        Assert.Equal(1.0, dr.NonDiscountedAvg, 6);
    }

    [Fact]
    public void Build_NotResponsive_WhenDiscountedAvgNotAboveThreshold()
    {
        var sales = new[]
        {
            Sale("L", "G", "S", "2026-06", 5, discounted: true, discountPct: 10),
            Sale("L", "G", "S", "2026-05", 5, discounted: true, discountPct: 10),
            Sale("L", "G", "S", "2026-04", 5, discounted: true, discountPct: 10),
            Sale("L", "G", "S", "2026-03", 5, discounted: true, discountPct: 10),
            Sale("L", "G", "S", "2026-02", 5, discounted: true, discountPct: 10),
            Sale("L", "G", "S", "2026-06", 5),
            Sale("L", "G", "S", "2026-05", 5),
            Sale("L", "G", "S", "2026-04", 5),
            Sale("L", "G", "S", "2026-03", 5),
            Sale("L", "G", "S", "2026-02", 5),
        };

        var dr = DiscountResponses.Build(sales)["G|S"];

        // da == na, so da > na*1.03 is false even with 5 discounted rows.
        Assert.False(dr.Responsive);
        Assert.Equal(5.0, dr.DiscountedAvg, 6);
        Assert.Equal(5.0, dr.NonDiscountedAvg, 6);
    }

    [Fact]
    public void Build_NoDiscountedRows_DefaultsSuggestTenAndRangeZero()
    {
        var sales = new[]
        {
            Sale("L", "E", "Q", "2026-06", 5),
            Sale("L", "E", "Q", "2026-05", 5),
            Sale("L", "E", "Q", "2026-04", 5),
        };

        var dr = DiscountResponses.Build(sales)["E|Q"];

        Assert.False(dr.Responsive);
        Assert.Equal(10, dr.Suggest);
        Assert.Equal("0%", dr.Range);
        Assert.Equal(0.0, dr.DiscountedAvg, 6);
        Assert.Equal(5.0, dr.NonDiscountedAvg, 6);
    }

    [Fact]
    public void Build_SuggestIsMinOfFifteenAndMedianPct()
    {
        var sales = new[]
        {
            // B|Y: distinct pcts [5,10] -> median index 2/2 = 1 -> 10; min(15,10) = 10.
            Sale("L", "B", "Y", "2026-06", 3, discounted: true, discountPct: 5),
            Sale("L", "B", "Y", "2026-05", 3, discounted: true, discountPct: 10),

            // C|Z: distinct pcts [20,30,40] -> median index 3/2 = 1 -> 30; min(15,30) = 15.
            Sale("L", "C", "Z", "2026-06", 3, discounted: true, discountPct: 20),
            Sale("L", "C", "Z", "2026-05", 3, discounted: true, discountPct: 30),
            Sale("L", "C", "Z", "2026-04", 3, discounted: true, discountPct: 40),
        };

        var result = DiscountResponses.Build(sales);

        Assert.Equal(10, result["B|Y"].Suggest);
        Assert.Equal("5%–10%", result["B|Y"].Range);
        Assert.Equal(15, result["C|Z"].Suggest);
        Assert.Equal("20%–40%", result["C|Z"].Range);
    }

    [Fact]
    public void Build_ZeroDiscountPctIsIgnoredForPctsButRowStillCountsAsDiscounted()
    {
        var sales = new[]
        {
            Sale("L", "D", "W", "2026-06", 8, discounted: true, discountPct: 0),
            Sale("L", "D", "W", "2026-05", 8, discounted: true, discountPct: 0),
        };

        var dr = DiscountResponses.Build(sales)["D|W"];

        // No positive pcts survive the p>0 filter, so defaults apply, but da is still computed.
        Assert.Equal(10, dr.Suggest);
        Assert.Equal("0%", dr.Range);
        Assert.Equal(8.0, dr.DiscountedAvg, 6);
        Assert.Equal(0.0, dr.NonDiscountedAvg, 6);
        Assert.False(dr.Responsive);
    }

    [Fact]
    public void Build_GroupsByModelVariant()
    {
        var sales = new[]
        {
            Sale("L1", "A", "X", "2026-06", 4),
            Sale("L2", "A", "X", "2026-06", 6),
            Sale("L1", "A", "Y", "2026-06", 2),
        };

        var result = DiscountResponses.Build(sales);

        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey("A|X"));
        Assert.True(result.ContainsKey("A|Y"));
        // A|X pools both locations: mean(4,6) = 5 on the non-discounted average.
        Assert.Equal(5.0, result["A|X"].NonDiscountedAvg, 6);
    }

    // ---------------------------------------------------------------------
    // InventoryRiskCalculator.Compute
    // ---------------------------------------------------------------------

    [Fact]
    public void Compute_EmptyInventory_ReturnsEmpty()
    {
        var result = InventoryRiskCalculator.Compute(
            Array.Empty<InventoryUnit>(), Array.Empty<SalesRow>(), Settings());

        Assert.Empty(result);
    }

    [Fact]
    public void Compute_NoMatchingSales_AgedUnit_PrioritiseLiquidation()
    {
        var units = new[] { Unit("S1", "L1", "M", "V", new DateOnly(2026, 1, 1), holdingCostPerDay: 100m) };

        var r = InventoryRiskCalculator.Compute(units, Array.Empty<SalesRow>(), Settings())[0];

        // 2026-01-01 -> 2026-06-30 is 180 days.
        Assert.Equal(180, r.InventoryAgeDays);
        Assert.Equal(180m * 100m, r.AccumulatedHoldingCost);
        Assert.Equal("Insufficient demand history", r.DemandBasis);
        Assert.Equal("Critical aging", r.AgingBand);
        Assert.Equal("Prioritise liquidation", r.Recommendation.Action);
        Assert.Equal("Medium", r.Recommendation.Confidence);
        Assert.InRange(r.RiskScore, 0, 100);
        Assert.Equal(Bands.Aging(r.InventoryAgeDays, Settings().AgingBands), r.AgingBand);
        Assert.Equal(Bands.Risk(r.RiskScore, Settings().RiskBands), r.RiskBand);
    }

    [Fact]
    public void Compute_NoMatchingSales_RecentUnit_InvestigateDemandData()
    {
        var units = new[] { Unit("S1", "L1", "M", "V", new DateOnly(2026, 6, 1), holdingCostPerDay: 50m) };

        var r = InventoryRiskCalculator.Compute(units, Array.Empty<SalesRow>(), Settings())[0];

        // 2026-06-01 -> 2026-06-30 is 29 days.
        Assert.Equal(29, r.InventoryAgeDays);
        Assert.Equal(29m * 50m, r.AccumulatedHoldingCost);
        Assert.Equal("Insufficient demand history", r.DemandBasis);
        Assert.Equal("New", r.AgingBand);
        Assert.Equal("Investigate demand data", r.Recommendation.Action);
        Assert.Equal("Low", r.Recommendation.Confidence);
    }

    [Fact]
    public void Compute_HealthyCoverRecentPositiveDemand_Retain()
    {
        var units = new[] { Unit("S1", "L1", "M", "V", new DateOnly(2026, 6, 1)) };
        var sales = new[]
        {
            Sale("L1", "M", "V", "2026-04", 4),
            Sale("L1", "M", "V", "2026-05", 5),
            Sale("L1", "M", "V", "2026-06", 6),
        };

        var r = InventoryRiskCalculator.Compute(units, sales, Settings())[0];

        Assert.Equal("Retain", r.Recommendation.Action);
        Assert.Equal("High", r.Recommendation.Confidence);
        Assert.Empty(r.Recommendation.Assumptions);
        Assert.Equal(3, r.Recommendation.Evidence.Count);
        // Trailing-3 mean of 6,5,4 = 5, cover = 1/5 = 0.2.
        Assert.Equal(5.0, r.Velocity, 6);
        Assert.Equal(0.2, r.StockCover, 6);
        Assert.Equal("Location-model-variant demand", r.DemandBasis);
    }

    [Fact]
    public void Compute_RiskScoreAndFactors_SatisfyInvariants()
    {
        var settings = Settings();
        var units = new[]
        {
            Unit("U1", "L1", "M", "V", new DateOnly(2026, 1, 1), new DateOnly(2025, 6, 1), holdingCostPerDay: 100m, leadTimeDays: 30),
            Unit("U2", "L1", "M", "V", new DateOnly(2026, 5, 1), new DateOnly(2026, 1, 1), holdingCostPerDay: 200m, leadTimeDays: 45),
            Unit("U3", "L2", "M", "W", new DateOnly(2026, 6, 10), new DateOnly(2025, 10, 1), holdingCostPerDay: 50m, leadTimeDays: 10),
            Unit("U4", "L2", "N", "V", new DateOnly(2026, 3, 1), new DateOnly(2025, 9, 1), holdingCostPerDay: 300m, leadTimeDays: 90),
        };
        var sales = new[]
        {
            Sale("L1", "M", "V", "2026-06", 3),
            Sale("L1", "M", "V", "2026-05", 3),
            Sale("L1", "M", "V", "2026-04", 3),
            Sale("L1", "M", "V", "2026-03", 2),
            Sale("L1", "M", "V", "2026-02", 2),
            Sale("L1", "M", "V", "2026-01", 2),
            Sale("L2", "M", "W", "2026-06", 10),
            Sale("L2", "M", "W", "2026-05", 10),
            Sale("L2", "M", "W", "2026-04", 10),
            Sale("L2", "N", "V", "2026-06", 1),
        };

        var results = InventoryRiskCalculator.Compute(units, sales, settings);

        Assert.Equal(4, results.Count);
        foreach (var r in results)
        {
            Assert.InRange(r.RiskScore, 0, 100);

            Assert.Equal(5, r.Factors.Count);
            Assert.All(r.Factors, f => Assert.True(f.Points >= 0, $"factor {f.Key} had negative points {f.Points}"));

            // Factors are sorted by Points descending.
            for (var i = 1; i < r.Factors.Count; i++)
            {
                Assert.True(r.Factors[i - 1].Points >= r.Factors[i].Points);
            }

            // InventoryAgeDays / ManufacturingAgeDays are analysis-date minus the respective date.
            Assert.Equal(settings.AnalysisDate.DayNumber - r.DateOfPurchase.DayNumber, r.InventoryAgeDays);
            Assert.Equal(settings.AnalysisDate.DayNumber - r.DateOfManufacture.DayNumber, r.ManufacturingAgeDays);

            // AccumulatedHoldingCost == max(0, invAge) * holdingCostPerDay.
            Assert.Equal((decimal)Math.Max(0, r.InventoryAgeDays) * r.HoldingCostPerDay, r.AccumulatedHoldingCost);

            // Bands are consistent with the Bands helper.
            Assert.Equal(Bands.Aging(r.InventoryAgeDays, settings.AgingBands), r.AgingBand);
            Assert.Equal(Bands.Manufacturing(r.ManufacturingAgeDays), r.ManufacturingBand);
            Assert.Equal(Bands.Risk(r.RiskScore, settings.RiskBands), r.RiskBand);
        }

        // GroupStock counts units sharing location|model|variant: U1 and U2 -> 2.
        Assert.All(results.Where(r => r.Location == "L1" && r.Model == "M" && r.Variant == "V"),
            r => Assert.Equal(2, r.GroupStock));
        Assert.Equal(1, results.Single(r => r.StockId == "U3").GroupStock);
    }

    [Fact]
    public void Compute_FactorsSortedByPointsDescending()
    {
        var units = new[] { Unit("S1", "L1", "M", "V", new DateOnly(2026, 2, 1), holdingCostPerDay: 500m) };
        var sales = new[]
        {
            Sale("L1", "M", "V", "2026-06", 1),
            Sale("L1", "M", "V", "2026-05", 2),
            Sale("L1", "M", "V", "2026-04", 3),
        };

        var r = InventoryRiskCalculator.Compute(units, sales, Settings())[0];

        var points = r.Factors.Select(f => f.Points).ToArray();
        var sorted = points.OrderByDescending(p => p).ToArray();
        Assert.Equal(sorted, points);
    }

    [Fact]
    public void Compute_FuturePurchaseDate_NegativeAge_YieldsZeroHoldingCost()
    {
        var units = new[] { Unit("S1", "L1", "M", "V", new DateOnly(2026, 8, 1), holdingCostPerDay: 100m) };

        var r = InventoryRiskCalculator.Compute(units, Array.Empty<SalesRow>(), Settings())[0];

        Assert.True(r.InventoryAgeDays < 0);
        Assert.Equal(0m, r.AccumulatedHoldingCost);
        Assert.InRange(r.RiskScore, 0, 100);
        Assert.All(r.Factors, f => Assert.True(f.Points >= 0));
    }

    [Fact]
    public void Compute_ElevatedCoverWithStrongerDemandElsewhere_TransferStock()
    {
        var units = new[]
        {
            Unit("A1", "L1", "M", "V", new DateOnly(2026, 5, 1)),
            Unit("A2", "L1", "M", "V", new DateOnly(2026, 5, 1)),
            Unit("A3", "L1", "M", "V", new DateOnly(2026, 5, 1)),
            Unit("A4", "L1", "M", "V", new DateOnly(2026, 5, 1)),
            Unit("B1", "L2", "M", "V", new DateOnly(2026, 5, 1)),
        };
        var months = new[] { "2026-01", "2026-02", "2026-03", "2026-04", "2026-05", "2026-06" };
        var sales = months
            .Select(m => Sale("L1", "M", "V", m, 0.5))
            .Concat(months.Select(m => Sale("L2", "M", "V", m, 10)))
            .ToArray();

        var results = InventoryRiskCalculator.Compute(units, sales, Settings());
        var source = results.First(r => r.Location == "L1");

        // L1 cover = 4 / 0.5 = 8 (> coverMax 6); L2 dest cover = 1 / 10 = 0.1 (< 8*0.6).
        Assert.Equal(8.0, source.StockCover, 6);
        Assert.Equal("Transfer stock", source.Recommendation.Action);
        Assert.Equal("L2", source.Recommendation.Destination);
    }

    [Fact]
    public void Compute_AgedResponsiveModelVariant_ApplyControlledDiscount()
    {
        var units = new[] { Unit("S1", "L1", "M", "V", new DateOnly(2026, 1, 1), holdingCostPerDay: 100m) };
        var sales = new[]
        {
            Sale("L1", "M", "V", "2026-06", 2, discounted: true, discountPct: 10),
            Sale("L1", "M", "V", "2026-06", 2, discounted: true, discountPct: 10),
            Sale("L1", "M", "V", "2026-05", 2, discounted: true, discountPct: 10),
            Sale("L1", "M", "V", "2026-05", 2, discounted: true, discountPct: 15),
            Sale("L1", "M", "V", "2026-04", 2, discounted: true, discountPct: 15),
            Sale("L1", "M", "V", "2026-06", 1),
        };

        var r = InventoryRiskCalculator.Compute(units, sales, Settings())[0];

        Assert.Equal("Apply controlled discount", r.Recommendation.Action);
        // distinct pcts [10,15] -> median index 1 -> 15 -> min(15,15) = 15.
        Assert.Equal(15, r.Recommendation.DiscountPct);
        Assert.Contains("Observed discount range: 10%–15%", r.Recommendation.Evidence);
    }

    [Fact]
    public void Compute_ElevatedAgeSofteningDemand_StartTargetedPromotion()
    {
        var units = new[] { Unit("S1", "L1", "M", "V", new DateOnly(2026, 3, 15)) };
        var months = new[] { "2026-01", "2026-02", "2026-03", "2026-04", "2026-05", "2026-06" };
        var sales = months.Select(m => Sale("L1", "M", "V", m, 3)).ToArray();

        var r = InventoryRiskCalculator.Compute(units, sales, Settings())[0];

        // 2026-03-15 -> 2026-06-30 is 107 days: above watch (90) but below critical (120).
        Assert.Equal(107, r.InventoryAgeDays);
        Assert.Equal("stable", r.TrendDirection);
        Assert.Equal("Start targeted promotion", r.Recommendation.Action);
    }

    [Fact]
    public void Compute_HighCoverFlatDemandNoTransfer_PauseReduceProcurement()
    {
        var units = new[]
        {
            Unit("S1", "L1", "M", "V", new DateOnly(2026, 5, 1)),
            Unit("S2", "L1", "M", "V", new DateOnly(2026, 5, 1)),
            Unit("S3", "L1", "M", "V", new DateOnly(2026, 5, 1)),
        };
        var months = new[] { "2026-01", "2026-02", "2026-03", "2026-04", "2026-05", "2026-06" };
        var sales = months.Select(m => Sale("L1", "M", "V", m, 0.4)).ToArray();

        var r = InventoryRiskCalculator.Compute(units, sales, Settings()).First(x => x.Location == "L1");

        // cover = 3 / 0.4 = 7.5 (> 6), demand flat, single location -> no transfer destination.
        Assert.True(r.StockCover > 6);
        Assert.Equal("stable", r.TrendDirection);
        Assert.Equal("Pause / reduce procurement", r.Recommendation.Action);
    }

    [Fact]
    public void Compute_ZeroWeights_ProducesZeroScoreAndZeroPoints()
    {
        var settings = Settings(new RiskWeights(0, 0, 0, 0, 0));
        var units = new[] { Unit("S1", "L1", "M", "V", new DateOnly(2026, 1, 1)) };
        var sales = new[]
        {
            Sale("L1", "M", "V", "2026-06", 5),
            Sale("L1", "M", "V", "2026-05", 5),
            Sale("L1", "M", "V", "2026-04", 5),
        };

        var r = InventoryRiskCalculator.Compute(units, sales, settings)[0];

        Assert.Equal(0, r.RiskScore);
        Assert.Equal("Low", r.RiskBand);
        Assert.All(r.Factors, f => Assert.Equal(0.0, f.Points, 6));
    }
}
