using BeeEye.Analytics.Demand;
using BeeEye.Analytics.Inventory;
using Xunit;

namespace BeeEye.Analytics.Tests;

/// <summary>
/// Unit tests for <see cref="InventoryAggregator.Aggregate"/>. Inputs are hand-built
/// <see cref="InventoryUnitRisk"/> records with known fields so every aggregate is
/// hand-computable; one integration test drives the real
/// <see cref="InventoryRiskCalculator.Compute"/> pipeline and asserts structural invariants.
/// </summary>
public sealed class InventoryAggregatorTests
{
    // ---------------------------------------------------------------------
    // Fixture factory: a single InventoryUnitRisk with sensible defaults.
    // Only the fields that influence Aggregate are exposed as parameters.
    // ---------------------------------------------------------------------
    private static InventoryUnitRisk Unit(
        decimal purchasePrice = 100m,
        decimal holdingCostPerDay = 5m,
        decimal accumulatedHoldingCost = 50m,
        int inventoryAgeDays = 40,
        int manufacturingAgeDays = 100,
        int leadTimeDays = 20,
        string agingBand = "New",
        string? manufacturingBand = null,
        string riskBand = "Low",
        string trendDirection = "stable",
        string action = "Retain",
        string model = "Camry",
        string location = "Riyadh",
        string variant = "LE",
        string brand = "Toyota",
        string colour = "White",
        string interior = "Beige")
    {
        // Compute the manufacturing band from the age via the same porter used by the
        // production code, so tests never hard-code the en-dash band literals.
        manufacturingBand ??= Bands.Manufacturing(manufacturingAgeDays);

        var recommendation = new InventoryRecommendation(
            Action: action,
            Confidence: "High",
            Why: "why",
            Evidence: Array.Empty<string>(),
            Outcome: "outcome",
            Assumptions: Array.Empty<string>());

        return new InventoryUnitRisk(
            StockId: "S",
            ChassisNo: "C",
            Brand: brand,
            Model: model,
            Variant: variant,
            Colour: colour,
            Interior: interior,
            Type: "Sedan",
            Location: location,
            DateOfPurchase: new DateOnly(2026, 1, 1),
            DateOfManufacture: new DateOnly(2025, 1, 1),
            ServiceDate: null,
            PurchasePrice: purchasePrice,
            HoldingCostPerDay: holdingCostPerDay,
            LeadTimeDays: leadTimeDays,
            InventoryAgeDays: inventoryAgeDays,
            ManufacturingAgeDays: manufacturingAgeDays,
            AccumulatedHoldingCost: accumulatedHoldingCost,
            AgingBand: agingBand,
            ManufacturingBand: manufacturingBand,
            Velocity: 1.0,
            DemandBasis: "lmv",
            DemandConfidence: "High",
            DemandDetail: "detail",
            StockCover: 2.0,
            GroupStock: 3,
            TrendDirection: trendDirection,
            TrendChangePct: 0.0,
            RiskScore: 10,
            RiskBand: riskBand,
            Factors: Array.Empty<RiskFactor>(),
            Recommendation: recommendation);
    }

    /// <summary>
    /// A five-unit portfolio with fully distinct, hand-computable values.
    ///
    /// U1  price 100  hold 5   acc 50    invAge 40   mfgAge 100  lead 20  Low       New              stable      Retain                        Camry/Riyadh/LE /Toyota /White /Beige
    /// U2  price 200  hold 10  acc 300   invAge 70   mfgAge 200  lead 30  Medium    Healthy          declining   Start targeted promotion      Camry/Riyadh/LE /Toyota /Black /Beige
    /// U3  price 300  hold 15  acc 900   invAge 100  mfgAge 300  lead 40  High      Watch            declining   Apply controlled discount     Corolla/Jeddah/XLI/Toyota /White /Black
    /// U4  price 400  hold 20  acc 2000  invAge 130  mfgAge 400  lead 50  Critical  Critical aging   declining   Transfer stock                Sonata/Jeddah/GLS/Hyundai/Silver/Black
    /// U5  price 150  hold 8   acc 100   invAge 55   mfgAge 250  lead 25  High      High attention   increasing  Pause / reduce procurement    Corolla/Riyadh/XLI/Toyota /White /Black
    /// </summary>
    private static List<InventoryUnitRisk> Portfolio() =>
    [
        Unit(purchasePrice: 100m, holdingCostPerDay: 5m, accumulatedHoldingCost: 50m,
            inventoryAgeDays: 40, manufacturingAgeDays: 100, leadTimeDays: 20,
            agingBand: "New", riskBand: "Low", trendDirection: "stable", action: "Retain",
            model: "Camry", location: "Riyadh", variant: "LE", brand: "Toyota", colour: "White", interior: "Beige"),
        Unit(purchasePrice: 200m, holdingCostPerDay: 10m, accumulatedHoldingCost: 300m,
            inventoryAgeDays: 70, manufacturingAgeDays: 200, leadTimeDays: 30,
            agingBand: "Healthy", riskBand: "Medium", trendDirection: "declining", action: "Start targeted promotion",
            model: "Camry", location: "Riyadh", variant: "LE", brand: "Toyota", colour: "Black", interior: "Beige"),
        Unit(purchasePrice: 300m, holdingCostPerDay: 15m, accumulatedHoldingCost: 900m,
            inventoryAgeDays: 100, manufacturingAgeDays: 300, leadTimeDays: 40,
            agingBand: "Watch", riskBand: "High", trendDirection: "declining", action: "Apply controlled discount",
            model: "Corolla", location: "Jeddah", variant: "XLI", brand: "Toyota", colour: "White", interior: "Black"),
        Unit(purchasePrice: 400m, holdingCostPerDay: 20m, accumulatedHoldingCost: 2000m,
            inventoryAgeDays: 130, manufacturingAgeDays: 400, leadTimeDays: 50,
            agingBand: "Critical aging", riskBand: "Critical", trendDirection: "declining", action: "Transfer stock",
            model: "Sonata", location: "Jeddah", variant: "GLS", brand: "Hyundai", colour: "Silver", interior: "Black"),
        Unit(purchasePrice: 150m, holdingCostPerDay: 8m, accumulatedHoldingCost: 100m,
            inventoryAgeDays: 55, manufacturingAgeDays: 250, leadTimeDays: 25,
            agingBand: "High attention", riskBand: "High", trendDirection: "increasing", action: "Pause / reduce procurement",
            model: "Corolla", location: "Riyadh", variant: "XLI", brand: "Toyota", colour: "White", interior: "Black"),
    ];

    private static void AssertDescendingByValue(IReadOnlyList<DimensionValue> dims)
    {
        for (var i = 1; i < dims.Count; i++)
        {
            Assert.True(dims[i - 1].Value >= dims[i].Value,
                $"Dimensions not sorted by Value descending at index {i}: {dims[i - 1].Value} < {dims[i].Value}");
        }
    }

    // ---------------------------------------------------------------------
    // Totals: Count, Value, holding-cost sums.
    // ---------------------------------------------------------------------
    [Fact]
    public void Aggregate_TotalsAndHoldingSums()
    {
        var summary = InventoryAggregator.Aggregate(Portfolio());

        Assert.Equal(5, summary.Count);
        Assert.Equal(1150m, summary.Value);                    // 100+200+300+400+150
        Assert.Equal(3350m, summary.AccumulatedHoldingCost);   // 50+300+900+2000+100
        Assert.Equal(58m, summary.DailyHoldingCost);           // 5+10+15+20+8
    }

    // ---------------------------------------------------------------------
    // Averages (Mean helper) — exercised on a non-empty set.
    // ---------------------------------------------------------------------
    [Fact]
    public void Aggregate_Averages()
    {
        var summary = InventoryAggregator.Aggregate(Portfolio());

        Assert.Equal(79d, summary.AverageInventoryAge, 6);       // (40+70+100+130+55)/5
        Assert.Equal(250d, summary.AverageManufacturingAge, 6);  // (100+200+300+400+250)/5
        Assert.Equal(33d, summary.AverageLeadTime, 6);           // (20+30+40+50+25)/5
    }

    // ---------------------------------------------------------------------
    // Risk-value / risk-count aggregates + declining value.
    // ---------------------------------------------------------------------
    [Fact]
    public void Aggregate_RiskValuesAndCounts()
    {
        var summary = InventoryAggregator.Aggregate(Portfolio());

        // High (U3=300, U5=150) + Critical (U4=400) = 850
        Assert.Equal(850m, summary.HighRiskValue);
        Assert.Equal(400m, summary.CriticalValue);   // U4
        Assert.Equal(1, summary.CriticalCount);       // U4
        Assert.Equal(2, summary.HighCount);           // U3, U5
        // declining: U2(200) + U3(300) + U4(400)
        Assert.Equal(900m, summary.DecliningValue);
    }

    // ---------------------------------------------------------------------
    // Recommendation action counts.
    // ---------------------------------------------------------------------
    [Fact]
    public void Aggregate_RecommendationCounts()
    {
        var summary = InventoryAggregator.Aggregate(Portfolio());

        Assert.Equal(1, summary.TransferCount);    // U4
        Assert.Equal(1, summary.PromotionCount);   // U2
        Assert.Equal(1, summary.DiscountCount);    // U3
        Assert.Equal(1, summary.PauseCount);       // U5
    }

    // ---------------------------------------------------------------------
    // ByRisk: exactly 4 entries keyed Low/Medium/High/Critical, in order,
    // with counts and values summing to the portfolio totals.
    // ---------------------------------------------------------------------
    [Fact]
    public void Aggregate_ByRisk_FourKeyedBandsSummingToTotals()
    {
        var summary = InventoryAggregator.Aggregate(Portfolio());
        var byRisk = summary.ByRisk;

        Assert.Equal(4, byRisk.Count);
        Assert.Equal(new[] { "Low", "Medium", "High", "Critical" }, byRisk.Select(b => b.Key).ToArray());

        Assert.Equal((1, 100m), (byRisk[0].Units, byRisk[0].Value));   // Low     -> U1
        Assert.Equal((1, 200m), (byRisk[1].Units, byRisk[1].Value));   // Medium  -> U2
        Assert.Equal((2, 450m), (byRisk[2].Units, byRisk[2].Value));   // High    -> U3+U5
        Assert.Equal((1, 400m), (byRisk[3].Units, byRisk[3].Value));   // Critical-> U4

        Assert.Equal(summary.Count, byRisk.Sum(b => b.Units));
        Assert.Equal(summary.Value, byRisk.Sum(b => b.Value));
    }

    // ---------------------------------------------------------------------
    // ByAging: exactly 5 entries in the fixed key order.
    // ---------------------------------------------------------------------
    [Fact]
    public void Aggregate_ByAging_FiveKeyedBands()
    {
        var summary = InventoryAggregator.Aggregate(Portfolio());
        var byAging = summary.ByAging;

        Assert.Equal(5, byAging.Count);
        Assert.Equal(
            new[] { "New", "Healthy", "Watch", "High attention", "Critical aging" },
            byAging.Select(b => b.Key).ToArray());

        Assert.Equal((1, 100m), (byAging[0].Units, byAging[0].Value));   // New            -> U1
        Assert.Equal((1, 200m), (byAging[1].Units, byAging[1].Value));   // Healthy        -> U2
        Assert.Equal((1, 300m), (byAging[2].Units, byAging[2].Value));   // Watch          -> U3
        Assert.Equal((1, 150m), (byAging[3].Units, byAging[3].Value));   // High attention -> U5
        Assert.Equal((1, 400m), (byAging[4].Units, byAging[4].Value));   // Critical aging -> U4

        Assert.Equal(summary.Count, byAging.Sum(b => b.Units));
        Assert.Equal(summary.Value, byAging.Sum(b => b.Value));
    }

    // ---------------------------------------------------------------------
    // ByManufacturing: exactly 4 entries in the fixed key order. Keys are
    // compared against Bands.Manufacturing(...) so the en-dash literals are
    // never hard-coded in the test.
    // ---------------------------------------------------------------------
    [Fact]
    public void Aggregate_ByManufacturing_FourKeyedBands()
    {
        var summary = InventoryAggregator.Aggregate(Portfolio());
        var byMfg = summary.ByManufacturing;

        Assert.Equal(4, byMfg.Count);
        Assert.Equal(
            new[]
            {
                Bands.Manufacturing(0),    // "0–180 days"
                Bands.Manufacturing(200),  // "181–270 days"
                Bands.Manufacturing(300),  // "271–365 days"
                Bands.Manufacturing(400),  // "365+ days"
            },
            byMfg.Select(b => b.Key).ToArray());

        Assert.Equal((1, 100m), (byMfg[0].Units, byMfg[0].Value));   // 0–180   -> U1(100)
        Assert.Equal((2, 350m), (byMfg[1].Units, byMfg[1].Value));   // 181–270 -> U2(200)+U5(150)
        Assert.Equal((1, 300m), (byMfg[2].Units, byMfg[2].Value));   // 271–365 -> U3(300)
        Assert.Equal((1, 400m), (byMfg[3].Units, byMfg[3].Value));   // 365+    -> U4(400)

        Assert.Equal(summary.Count, byMfg.Sum(b => b.Units));
        Assert.Equal(summary.Value, byMfg.Sum(b => b.Value));
    }

    // ---------------------------------------------------------------------
    // ByModel dimension: grouped, sorted by Value descending, carrying
    // accumulated holding cost per group.
    // ---------------------------------------------------------------------
    [Fact]
    public void Aggregate_ByModel_SortedByValueDescending()
    {
        var summary = InventoryAggregator.Aggregate(Portfolio());
        var byModel = summary.ByModel;

        Assert.Equal(3, byModel.Count);
        AssertDescendingByValue(byModel);

        // Corolla: U3(300)+U5(150)=450, acc 900+100=1000
        Assert.Equal("Corolla", byModel[0].Key);
        Assert.Equal(2, byModel[0].Units);
        Assert.Equal(450m, byModel[0].Value);
        Assert.Equal(1000m, byModel[0].AccumulatedHoldingCost);

        // Sonata: U4(400)=400, acc 2000
        Assert.Equal("Sonata", byModel[1].Key);
        Assert.Equal(1, byModel[1].Units);
        Assert.Equal(400m, byModel[1].Value);
        Assert.Equal(2000m, byModel[1].AccumulatedHoldingCost);

        // Camry: U1(100)+U2(200)=300, acc 50+300=350
        Assert.Equal("Camry", byModel[2].Key);
        Assert.Equal(2, byModel[2].Units);
        Assert.Equal(300m, byModel[2].Value);
        Assert.Equal(350m, byModel[2].AccumulatedHoldingCost);

        Assert.Equal(summary.Value, byModel.Sum(d => d.Value));
        Assert.Equal(summary.Count, byModel.Sum(d => d.Units));
        Assert.Equal(summary.AccumulatedHoldingCost, byModel.Sum(d => d.AccumulatedHoldingCost));
    }

    // ---------------------------------------------------------------------
    // ByLocation dimension: grouped, sorted by Value descending.
    // ---------------------------------------------------------------------
    [Fact]
    public void Aggregate_ByLocation_SortedByValueDescending()
    {
        var summary = InventoryAggregator.Aggregate(Portfolio());
        var byLoc = summary.ByLocation;

        Assert.Equal(2, byLoc.Count);
        AssertDescendingByValue(byLoc);

        // Jeddah: U3(300)+U4(400)=700, acc 900+2000=2900
        Assert.Equal("Jeddah", byLoc[0].Key);
        Assert.Equal(2, byLoc[0].Units);
        Assert.Equal(700m, byLoc[0].Value);
        Assert.Equal(2900m, byLoc[0].AccumulatedHoldingCost);

        // Riyadh: U1(100)+U2(200)+U5(150)=450, acc 50+300+100=450
        Assert.Equal("Riyadh", byLoc[1].Key);
        Assert.Equal(3, byLoc[1].Units);
        Assert.Equal(450m, byLoc[1].Value);
        Assert.Equal(450m, byLoc[1].AccumulatedHoldingCost);

        Assert.Equal(summary.Value, byLoc.Sum(d => d.Value));
        Assert.Equal(summary.Count, byLoc.Sum(d => d.Units));
    }

    // ---------------------------------------------------------------------
    // Remaining dimensions: Variant / Brand / Colour / Interior.
    // ---------------------------------------------------------------------
    [Fact]
    public void Aggregate_ByVariant_SortedByValueDescending()
    {
        var summary = InventoryAggregator.Aggregate(Portfolio());
        var byVariant = summary.ByVariant;

        Assert.Equal(3, byVariant.Count);
        AssertDescendingByValue(byVariant);

        Assert.Equal("XLI", byVariant[0].Key);   // U3(300)+U5(150)=450
        Assert.Equal(450m, byVariant[0].Value);
        Assert.Equal("GLS", byVariant[1].Key);   // U4(400)
        Assert.Equal(400m, byVariant[1].Value);
        Assert.Equal("LE", byVariant[2].Key);    // U1(100)+U2(200)=300
        Assert.Equal(300m, byVariant[2].Value);
    }

    [Fact]
    public void Aggregate_ByBrand_SortedByValueDescending()
    {
        var summary = InventoryAggregator.Aggregate(Portfolio());
        var byBrand = summary.ByBrand;

        Assert.Equal(2, byBrand.Count);
        AssertDescendingByValue(byBrand);

        Assert.Equal("Toyota", byBrand[0].Key);   // 100+200+300+150=750
        Assert.Equal(4, byBrand[0].Units);
        Assert.Equal(750m, byBrand[0].Value);
        Assert.Equal("Hyundai", byBrand[1].Key);  // 400
        Assert.Equal(1, byBrand[1].Units);
        Assert.Equal(400m, byBrand[1].Value);
    }

    [Fact]
    public void Aggregate_ByColour_SortedByValueDescending()
    {
        var summary = InventoryAggregator.Aggregate(Portfolio());
        var byColour = summary.ByColour;

        Assert.Equal(3, byColour.Count);
        AssertDescendingByValue(byColour);

        Assert.Equal("White", byColour[0].Key);   // U1(100)+U3(300)+U5(150)=550
        Assert.Equal(550m, byColour[0].Value);
        Assert.Equal("Silver", byColour[1].Key);  // U4(400)
        Assert.Equal(400m, byColour[1].Value);
        Assert.Equal("Black", byColour[2].Key);   // U2(200)
        Assert.Equal(200m, byColour[2].Value);
    }

    [Fact]
    public void Aggregate_ByInterior_SortedByValueDescending()
    {
        var summary = InventoryAggregator.Aggregate(Portfolio());
        var byInterior = summary.ByInterior;

        Assert.Equal(2, byInterior.Count);
        AssertDescendingByValue(byInterior);

        Assert.Equal("Black", byInterior[0].Key);  // U3(300)+U4(400)+U5(150)=850
        Assert.Equal(3, byInterior[0].Units);
        Assert.Equal(850m, byInterior[0].Value);
        Assert.Equal("Beige", byInterior[1].Key);  // U1(100)+U2(200)=300
        Assert.Equal(2, byInterior[1].Units);
        Assert.Equal(300m, byInterior[1].Value);
    }

    // ---------------------------------------------------------------------
    // Single-unit sanity: all buckets funnel to exactly one entry / band.
    // ---------------------------------------------------------------------
    [Fact]
    public void Aggregate_SingleUnit_FunnelsToOneDimensionEntry()
    {
        var summary = InventoryAggregator.Aggregate([Unit(purchasePrice: 42m)]);

        Assert.Equal(1, summary.Count);
        Assert.Equal(42m, summary.Value);
        Assert.Single(summary.ByModel);
        Assert.Single(summary.ByLocation);
        Assert.Equal(42m, summary.ByModel[0].Value);
        // Averages equal the single unit's raw metrics (no division surprises).
        Assert.Equal(40d, summary.AverageInventoryAge, 6);
        Assert.Equal(100d, summary.AverageManufacturingAge, 6);
        Assert.Equal(20d, summary.AverageLeadTime, 6);
    }

    // ---------------------------------------------------------------------
    // Empty input: zero totals, band skeletons preserved, no divide-by-zero.
    // ---------------------------------------------------------------------
    [Fact]
    public void Aggregate_Empty_ReturnsZeroTotalsAndBandSkeleton()
    {
        var summary = InventoryAggregator.Aggregate(Array.Empty<InventoryUnitRisk>());

        Assert.Equal(0, summary.Count);
        Assert.Equal(0m, summary.Value);
        Assert.Equal(0m, summary.AccumulatedHoldingCost);
        Assert.Equal(0m, summary.DailyHoldingCost);
        Assert.Equal(0m, summary.HighRiskValue);
        Assert.Equal(0m, summary.CriticalValue);
        Assert.Equal(0m, summary.DecliningValue);
        Assert.Equal(0, summary.CriticalCount);
        Assert.Equal(0, summary.HighCount);
        Assert.Equal(0, summary.TransferCount);
        Assert.Equal(0, summary.PromotionCount);
        Assert.Equal(0, summary.DiscountCount);
        Assert.Equal(0, summary.PauseCount);

        // Mean returns 0 for an empty set (guards divide-by-zero).
        Assert.Equal(0d, summary.AverageInventoryAge, 6);
        Assert.Equal(0d, summary.AverageManufacturingAge, 6);
        Assert.Equal(0d, summary.AverageLeadTime, 6);

        // Fixed banding skeletons are always emitted, every bucket empty.
        Assert.Equal(4, summary.ByRisk.Count);
        Assert.Equal(5, summary.ByAging.Count);
        Assert.Equal(4, summary.ByManufacturing.Count);
        Assert.All(summary.ByRisk, b => Assert.Equal((0, 0m), (b.Units, b.Value)));
        Assert.All(summary.ByAging, b => Assert.Equal((0, 0m), (b.Units, b.Value)));
        Assert.All(summary.ByManufacturing, b => Assert.Equal((0, 0m), (b.Units, b.Value)));

        // Free-dimension groupings collapse to empty lists.
        Assert.Empty(summary.ByLocation);
        Assert.Empty(summary.ByModel);
        Assert.Empty(summary.ByVariant);
        Assert.Empty(summary.ByBrand);
        Assert.Empty(summary.ByColour);
        Assert.Empty(summary.ByInterior);
    }

    // ---------------------------------------------------------------------
    // Integration: risk-score the units through the real calculator, then
    // aggregate. Only structural invariants are asserted (scores are the
    // subject of the calculator's own tests).
    // ---------------------------------------------------------------------
    [Fact]
    public void Aggregate_FromRiskCalculatorPipeline_Invariants()
    {
        var settings = RiskSettings.Default;      // AnalysisDate 2026-06-30

        var inventory = new List<InventoryUnit>
        {
            new(
                StockId: "A1", ChassisNo: "CH-A1", Brand: "Toyota", Model: "Camry", Variant: "LE",
                Colour: "White", Interior: "Beige", Type: "Sedan", Location: "Riyadh",
                DateOfPurchase: new DateOnly(2026, 1, 1), DateOfManufacture: new DateOnly(2025, 6, 1),
                ServiceDate: null, PurchasePrice: 120_000m, HoldingCostPerDay: 30m, LeadTimeDays: 20),
            new(
                StockId: "A2", ChassisNo: "CH-A2", Brand: "Hyundai", Model: "Sonata", Variant: "GLS",
                Colour: "Black", Interior: "Black", Type: "Sedan", Location: "Jeddah",
                DateOfPurchase: new DateOnly(2025, 9, 1), DateOfManufacture: new DateOnly(2024, 12, 1),
                ServiceDate: null, PurchasePrice: 95_000m, HoldingCostPerDay: 25m, LeadTimeDays: 35),
        };

        var sales = new List<SalesRow>
        {
            new("Riyadh", "Camry", "LE", "2026-04", 5),
            new("Riyadh", "Camry", "LE", "2026-05", 4),
            new("Riyadh", "Camry", "LE", "2026-06", 6),
            new("Jeddah", "Sonata", "GLS", "2026-04", 1),
            new("Jeddah", "Sonata", "GLS", "2026-05", 0),
            new("Jeddah", "Sonata", "GLS", "2026-06", 1),
        };

        var scored = InventoryRiskCalculator.Compute(inventory, sales, settings);
        var summary = InventoryAggregator.Aggregate(scored);

        // Count and value flow straight through from the scored units.
        Assert.Equal(inventory.Count, summary.Count);
        Assert.Equal(inventory.Sum(u => u.PurchasePrice), summary.Value);
        Assert.Equal(inventory.Sum(u => u.HoldingCostPerDay), summary.DailyHoldingCost);
        Assert.Equal(scored.Sum(u => u.AccumulatedHoldingCost), summary.AccumulatedHoldingCost);

        // Banding skeletons intact; every unit lands in exactly one band per axis.
        Assert.Equal(4, summary.ByRisk.Count);
        Assert.Equal(5, summary.ByAging.Count);
        Assert.Equal(4, summary.ByManufacturing.Count);
        Assert.Equal(summary.Count, summary.ByRisk.Sum(b => b.Units));
        Assert.Equal(summary.Count, summary.ByAging.Sum(b => b.Units));
        Assert.Equal(summary.Count, summary.ByManufacturing.Sum(b => b.Units));
        Assert.Equal(summary.Value, summary.ByRisk.Sum(b => b.Value));

        // Free dimensions are non-empty and Value-descending.
        Assert.NotEmpty(summary.ByModel);
        AssertDescendingByValue(summary.ByModel);
        AssertDescendingByValue(summary.ByLocation);
        Assert.Equal(summary.Value, summary.ByModel.Sum(d => d.Value));

        // High-risk value is the sum of High + Critical bands and never exceeds total.
        Assert.Equal(summary.ByRisk[2].Value + summary.ByRisk[3].Value, summary.HighRiskValue);
        Assert.True(summary.HighRiskValue <= summary.Value);
    }
}
