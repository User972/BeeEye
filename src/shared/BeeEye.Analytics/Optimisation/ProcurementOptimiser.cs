namespace BeeEye.Analytics.Optimisation;

/// <summary>Configurable procurement policy for UC4 (defaults are documented assumptions).</summary>
public sealed record ProcurementSettings
{
    public double ServiceLevel { get; init; } = 0.95;
    public double LeadTimeMonths { get; init; } = 2.0;
    public double LeadTimeStdMonths { get; init; } = 0.5;
    public double ReviewPeriodMonths { get; init; } = 1.0;
    public int MinOrderQuantity { get; init; }
    public int OrderMultiple { get; init; } = 1;

    public static ProcurementSettings Default => new();
}

public sealed record ProcurementRecommendation(
    double DemandMean,
    double DemandStd,
    double SafetyStock,
    double ReorderPoint,
    double OrderUpToLevel,
    int Available,
    int RecommendedQuantity,
    int RangeLow,
    int RangeHigh,
    string StockoutRisk,
    string Confidence,
    string Rationale);

/// <summary>
/// Procurement quantity optimisation (UC4): safety stock for a target service level,
/// reorder point and an order-up-to level, expressed as a recommended <b>range</b>
/// rather than a false-precision point. Inbound inventory is always netted off.
/// </summary>
public static class ProcurementOptimiser
{
    // One-sided normal z for common service levels.
    private static readonly (double Level, double Z)[] ZTable =
    [
        (0.80, 0.8416), (0.85, 1.0364), (0.90, 1.2816), (0.95, 1.6449), (0.975, 1.9600), (0.99, 2.3263),
    ];

    public static double Z(double serviceLevel)
        => ZTable.MinBy(t => Math.Abs(t.Level - serviceLevel)).Z;

    public static ProcurementRecommendation Recommend(
        double demandMeanPerMonth, double demandStdPerMonth, int currentInventory, int inbound,
        ProcurementSettings settings, string confidence = "Medium")
    {
        var leadPlusReview = settings.LeadTimeMonths + settings.ReviewPeriodMonths;

        // Variance from demand variability over the protection interval plus lead-time variability.
        var demandVariance = demandStdPerMonth * demandStdPerMonth * leadPlusReview;
        var leadVariance = demandMeanPerMonth * demandMeanPerMonth * settings.LeadTimeStdMonths * settings.LeadTimeStdMonths;
        var sigma = Math.Sqrt(demandVariance + leadVariance);

        var zService = Z(settings.ServiceLevel);
        var safetyStock = zService * sigma;
        var reorderPoint = (demandMeanPerMonth * settings.LeadTimeMonths) + safetyStock;
        var orderUpTo = (demandMeanPerMonth * leadPlusReview) + safetyStock;
        var available = currentInventory + inbound;

        // The recommended range is a 90–99% service band, widened to include the requested
        // service level when it sits outside that band — so the point estimate (which uses
        // zService) is always bracketed by [low, high].
        var zLow = Math.Min(Z(0.90), zService);
        var zHigh = Math.Max(Z(0.99), zService);

        var point = ApplyLotSizing(Math.Max(0, orderUpTo - available), settings);
        var low = ApplyLotSizing(Math.Max(0, (demandMeanPerMonth * leadPlusReview) + (zLow * sigma) - available), settings);
        var high = ApplyLotSizing(Math.Max(0, (demandMeanPerMonth * leadPlusReview) + (zHigh * sigma) - available), settings);

        var stockoutRisk = available < demandMeanPerMonth * settings.LeadTimeMonths ? "High"
            : available < reorderPoint ? "Medium"
            : "Low";

        var rationale =
            $"At a {settings.ServiceLevel:P0} service level over {leadPlusReview:0.#} months (lead + review), safety stock is " +
            $"{safetyStock:0} and the order-up-to level is {orderUpTo:0}; netting {available} available gives a recommended " +
            $"range of {low}–{high} units (point estimate {point}).";

        return new ProcurementRecommendation(
            demandMeanPerMonth, demandStdPerMonth, safetyStock, reorderPoint, orderUpTo,
            available, point, low, high, stockoutRisk, confidence, rationale);
    }

    private static int ApplyLotSizing(double quantity, ProcurementSettings s)
    {
        var qty = (int)Math.Ceiling(quantity);
        if (qty <= 0)
        {
            return 0;
        }

        qty = Math.Max(qty, s.MinOrderQuantity);
        if (s.OrderMultiple > 1)
        {
            qty = (int)Math.Ceiling(qty / (double)s.OrderMultiple) * s.OrderMultiple;
        }

        return qty;
    }
}
