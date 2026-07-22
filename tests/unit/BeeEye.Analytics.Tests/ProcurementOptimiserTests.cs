using System;
using BeeEye.Analytics.Optimisation;
using Xunit;

namespace BeeEye.Analytics.Tests;

/// <summary>
/// Deterministic unit tests for <see cref="ProcurementOptimiser"/>.
///
/// Canonical worked example (used by many tests):
///   mean = 10, std = 3, lead = 2, leadStd = 0.5, review = 1, service = 0.95, current = 5, inbound = 0.
///
///   leadPlusReview = 2 + 1 = 3
///   demandVariance = std^2 * leadPlusReview = 9 * 3 = 27
///   leadVariance   = mean^2 * leadStd^2     = 100 * 0.25 = 25
///   sigma          = sqrt(27 + 25) = sqrt(52) = 7.211102550927978
///
///   safety(0.95)   = Z(0.95) * sigma = 1.6449 * 7.211102550927978 = 11.861542586021431
///   reorderPoint   = mean*lead + safety = 20 + 11.861542586021431 = 31.861542586021431
///   orderUpTo      = mean*(lead+review) + safety = 30 + 11.861542586021431 = 41.861542586021431
///   available      = current + inbound = 5
///   point          = ceil(41.861542586 - 5)               = ceil(36.861542586) = 37
///   low  (Z 0.90)  = ceil(30 + 1.2816*sigma - 5)          = ceil(34.241749029) = 35
///   high (Z 0.99)  = ceil(30 + 2.3263*sigma - 5)          = ceil(41.775187864) = 42
/// </summary>
public class ProcurementOptimiserTests
{
    private const double Sigma = 7.211102550927978;          // sqrt(52)
    private const double Safety95 = 11.861542586021431;      // 1.6449 * sigma
    private const double ReorderPoint = 31.861542586021431;  // 20 + safety
    private const double OrderUpTo = 41.861542586021431;      // 30 + safety

    // The Default settings already match the canonical example's policy parameters.
    private static ProcurementSettings Defaults => ProcurementSettings.Default;

    // -------------------------------------------------------------------------
    // Z(serviceLevel) — nearest tabulated one-sided normal z.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0.95, 1.6449)]   // exact
    [InlineData(0.93, 1.6449)]   // |.93-.90|=.03 > |.93-.95|=.02 -> 0.95
    [InlineData(0.80, 0.8416)]   // exact (table minimum)
    [InlineData(0.85, 1.0364)]   // exact
    [InlineData(0.90, 1.2816)]   // exact
    [InlineData(0.975, 1.9600)]  // exact
    [InlineData(0.99, 2.3263)]   // exact (table maximum)
    [InlineData(0.50, 0.8416)]   // below table -> clamps to nearest 0.80
    [InlineData(0.999, 2.3263)]  // above table -> clamps to nearest 0.99
    [InlineData(0.87, 1.0364)]   // |.87-.85|=.02 < |.87-.90|=.03 -> 0.85
    [InlineData(0.92, 1.2816)]   // |.92-.90|=.02 < |.92-.95|=.03 -> 0.90
    public void Z_PicksNearestTabulatedLevel(double serviceLevel, double expected)
    {
        Assert.Equal(expected, ProcurementOptimiser.Z(serviceLevel), 6);
    }

    [Fact]
    public void Z_HigherServiceLevel_ProducesLargerOrEqualZ()
    {
        Assert.True(ProcurementOptimiser.Z(0.90) < ProcurementOptimiser.Z(0.95));
        Assert.True(ProcurementOptimiser.Z(0.95) < ProcurementOptimiser.Z(0.99));
    }

    // -------------------------------------------------------------------------
    // Recommend — canonical worked example (all fields verified by hand).
    // -------------------------------------------------------------------------

    [Fact]
    public void Recommend_CanonicalExample_ComputesAllFields()
    {
        var r = ProcurementOptimiser.Recommend(
            demandMeanPerMonth: 10, demandStdPerMonth: 3,
            currentInventory: 5, inbound: 0, settings: Defaults);

        Assert.Equal(10.0, r.DemandMean, 6);
        Assert.Equal(3.0, r.DemandStd, 6);
        Assert.Equal(Safety95, r.SafetyStock, 6);
        Assert.Equal(ReorderPoint, r.ReorderPoint, 6);
        Assert.Equal(OrderUpTo, r.OrderUpToLevel, 6);
        Assert.Equal(5, r.Available);
        Assert.Equal(37, r.RecommendedQuantity);
        Assert.Equal(35, r.RangeLow);
        Assert.Equal(42, r.RangeHigh);
        Assert.Equal("High", r.StockoutRisk); // available 5 < mean*lead 20
        Assert.Equal("Medium", r.Confidence);  // default confidence argument
    }

    [Fact]
    public void Recommend_CanonicalExample_RangeBracketsPointEstimate()
    {
        var r = ProcurementOptimiser.Recommend(10, 3, 5, 0, Defaults);

        // Low uses Z(0.90), High uses Z(0.99); point uses Z(serviceLevel)=Z(0.95).
        Assert.True(r.RangeLow <= r.RecommendedQuantity);
        Assert.True(r.RecommendedQuantity <= r.RangeHigh);
    }

    [Theory]
    [InlineData(0.80)] // Z below the 0.90 band lower bound
    [InlineData(0.85)] // "
    [InlineData(0.999)] // clamps to Z(0.99) upper bound
    public void Recommend_ServiceLevelOutsideBand_PointStillWithinRange(double serviceLevel)
    {
        // For a service level whose z sits outside the fixed 0.90–0.99 range band, the range
        // is widened to include it, so the point estimate never falls outside [low, high].
        var r = ProcurementOptimiser.Recommend(10, 3, 5, 0, Defaults with { ServiceLevel = serviceLevel });

        Assert.True(r.RangeLow <= r.RecommendedQuantity, $"low {r.RangeLow} > point {r.RecommendedQuantity}");
        Assert.True(r.RecommendedQuantity <= r.RangeHigh, $"point {r.RecommendedQuantity} > high {r.RangeHigh}");
    }

    [Fact]
    public void Recommend_SafetyStockFormula_MatchesZTimesSigma()
    {
        // safety = Z(service) * sqrt(std^2*(lead+review) + mean^2*leadStd^2)
        var r = ProcurementOptimiser.Recommend(10, 3, 5, 0, Defaults);
        Assert.Equal(ProcurementOptimiser.Z(0.95) * Sigma, r.SafetyStock, 6);
    }

    // -------------------------------------------------------------------------
    // Higher service level => larger safety stock (sigma is unchanged).
    // -------------------------------------------------------------------------

    [Fact]
    public void Recommend_HigherServiceLevel_IncreasesSafetyStock()
    {
        var s90 = Defaults with { ServiceLevel = 0.90 };
        var s95 = Defaults with { ServiceLevel = 0.95 };
        var s99 = Defaults with { ServiceLevel = 0.99 };

        var r90 = ProcurementOptimiser.Recommend(10, 3, 5, 0, s90);
        var r95 = ProcurementOptimiser.Recommend(10, 3, 5, 0, s95);
        var r99 = ProcurementOptimiser.Recommend(10, 3, 5, 0, s99);

        Assert.True(r90.SafetyStock < r95.SafetyStock);
        Assert.True(r95.SafetyStock < r99.SafetyStock);

        // Concrete hand-computed values at the extremes.
        Assert.Equal(1.2816 * Sigma, r90.SafetyStock, 6); // 9.241749029269217
        Assert.Equal(2.3263 * Sigma, r99.SafetyStock, 6); // 16.775187863923756

        // Reorder point and order-up-to shift by the same delta as safety stock.
        Assert.Equal(20 + r90.SafetyStock, r90.ReorderPoint, 6);
        Assert.Equal(30 + r99.SafetyStock, r99.OrderUpToLevel, 6);
    }

    // -------------------------------------------------------------------------
    // StockoutRisk branches (thresholds: mean*lead = 20, reorderPoint = 31.8615).
    // -------------------------------------------------------------------------

    [Fact]
    public void Recommend_StockoutRisk_HighWhenBelowLeadTimeDemand()
    {
        // available 5 < mean*lead 20 -> High
        var r = ProcurementOptimiser.Recommend(10, 3, 5, 0, Defaults);
        Assert.Equal("High", r.StockoutRisk);
    }

    [Fact]
    public void Recommend_StockoutRisk_MediumBetweenLeadDemandAndReorderPoint()
    {
        // available 25: not < 20 (not High), 25 < 31.8615 -> Medium
        var r = ProcurementOptimiser.Recommend(10, 3, 25, 0, Defaults);
        Assert.Equal("Medium", r.StockoutRisk);
    }

    [Fact]
    public void Recommend_StockoutRisk_MediumAtLeadTimeDemandBoundary()
    {
        // available exactly 20 == mean*lead: 20 < 20 is false -> not High; 20 < reorder -> Medium
        var r = ProcurementOptimiser.Recommend(10, 3, 20, 0, Defaults);
        Assert.Equal("Medium", r.StockoutRisk);
    }

    [Fact]
    public void Recommend_StockoutRisk_LowWhenAtOrAboveReorderPoint()
    {
        // available 40 >= reorderPoint 31.8615 -> Low
        var r = ProcurementOptimiser.Recommend(10, 3, 40, 0, Defaults);
        Assert.Equal("Low", r.StockoutRisk);
    }

    // -------------------------------------------------------------------------
    // Recommended quantity / lot sizing.
    // -------------------------------------------------------------------------

    [Fact]
    public void Recommend_WhenAmpleInventory_RecommendsZero()
    {
        // available 100 far exceeds orderUpTo 41.86 -> point, low, high all clamp to 0.
        var r = ProcurementOptimiser.Recommend(10, 3, 100, 0, Defaults);

        Assert.Equal(100, r.Available);
        Assert.Equal(0, r.RecommendedQuantity);
        Assert.Equal(0, r.RangeLow);
        Assert.Equal(0, r.RangeHigh);
        Assert.Equal("Low", r.StockoutRisk);
    }

    [Fact]
    public void Recommend_InboundIsNettedOffAvailable()
    {
        // current 5 + inbound 10 => available 15; point = ceil(41.8615 - 15) = 27.
        var r = ProcurementOptimiser.Recommend(10, 3, 5, 10, Defaults);

        Assert.Equal(15, r.Available);
        Assert.Equal(27, r.RecommendedQuantity);
    }

    [Fact]
    public void Recommend_MinOrderQuantity_RaisesPositiveQuantities()
    {
        // Raw: point 37, low 35, high 42. MinOrder 40 lifts 37->40 and 35->40; 42 stays.
        var settings = Defaults with { MinOrderQuantity = 40 };
        var r = ProcurementOptimiser.Recommend(10, 3, 5, 0, settings);

        Assert.Equal(40, r.RecommendedQuantity);
        Assert.Equal(40, r.RangeLow);
        Assert.Equal(42, r.RangeHigh);
    }

    [Fact]
    public void Recommend_MinOrderQuantity_DoesNotCreateOrderFromZero()
    {
        // Ample inventory keeps quantity at 0 even with a min-order floor (floor only applies when qty>0).
        var settings = Defaults with { MinOrderQuantity = 50 };
        var r = ProcurementOptimiser.Recommend(10, 3, 100, 0, settings);

        Assert.Equal(0, r.RecommendedQuantity);
        Assert.Equal(0, r.RangeLow);
        Assert.Equal(0, r.RangeHigh);
    }

    [Fact]
    public void Recommend_OrderMultiple_RoundsUpToNearestMultiple()
    {
        // Raw: point 37 -> ceil(3.7)*10 = 40; low 35 -> ceil(3.5)*10 = 40; high 42 -> ceil(4.2)*10 = 50.
        var settings = Defaults with { OrderMultiple = 10 };
        var r = ProcurementOptimiser.Recommend(10, 3, 5, 0, settings);

        Assert.Equal(40, r.RecommendedQuantity);
        Assert.Equal(40, r.RangeLow);
        Assert.Equal(50, r.RangeHigh);
    }

    [Fact]
    public void Recommend_OrderMultipleOfOne_LeavesQuantityUnchanged()
    {
        // Default OrderMultiple is 1 -> no rounding beyond the ceiling in lot sizing.
        var r = ProcurementOptimiser.Recommend(10, 3, 5, 0, Defaults);
        Assert.Equal(37, r.RecommendedQuantity);
    }

    // -------------------------------------------------------------------------
    // Miscellaneous passthroughs.
    // -------------------------------------------------------------------------

    [Fact]
    public void Recommend_ConfidenceArgument_IsPassedThrough()
    {
        var r = ProcurementOptimiser.Recommend(10, 3, 5, 0, Defaults, confidence: "High");
        Assert.Equal("High", r.Confidence);
    }

    [Fact]
    public void Recommend_Rationale_MentionsServiceLevelAndRange()
    {
        var r = ProcurementOptimiser.Recommend(10, 3, 5, 0, Defaults);

        Assert.False(string.IsNullOrWhiteSpace(r.Rationale));
        Assert.Contains("service level", r.Rationale);
        Assert.Contains("recommended", r.Rationale);
    }

    [Fact]
    public void ProcurementSettings_Default_HasDocumentedValues()
    {
        var d = ProcurementSettings.Default;
        Assert.Equal(0.95, d.ServiceLevel, 6);
        Assert.Equal(2.0, d.LeadTimeMonths, 6);
        Assert.Equal(0.5, d.LeadTimeStdMonths, 6);
        Assert.Equal(1.0, d.ReviewPeriodMonths, 6);
        Assert.Equal(0, d.MinOrderQuantity);
        Assert.Equal(1, d.OrderMultiple);
    }
}
