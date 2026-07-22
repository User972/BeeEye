using System.Globalization;
using BeeEye.Analytics.Optimisation;

namespace BeeEye.Analytics.SpareParts;

/// <summary>
/// UC7 stocking recommender. Turns an intermittent demand forecast plus the supply position
/// (lead time, on-hand, inbound) into a recommended stocking <b>range</b> and reorder considerations —
/// never false precision, never a fabricated number for insufficient-data parts. Safety-stock and
/// service-level logic reuse <see cref="ProcurementOptimiser"/> (UC4), adapted for intermittent series.
/// </summary>
public static class SparePartsForecaster
{
    private const double DaysPerMonth = 30.44;

    public static SparePartRecommendation Recommend(
        SparePartInput part, IReadOnlyList<double> usageSeries, SparePartsSettings? settings = null)
    {
        settings ??= SparePartsSettings.Default;
        var forecast = Intermittent.Forecast(usageSeries, settings);
        var leadTimeMonths = part.LeadTimeDays / DaysPerMonth;
        var available = part.CurrentStock + part.InboundStock;

        if (forecast.InsufficientData)
        {
            var insufficientRationale =
                $"Only {forecast.NonZeroPeriods} non-zero month(s) over {forecast.Periods} months of history — " +
                "below the minimum to forecast reliably. No stocking target is fabricated; collect more usage data " +
                "or stock by engineering judgement.";
            return new SparePartRecommendation(
                part.PartNumber, part.Name, part.Category, forecast.Class, forecast.Method,
                forecast.Adi, forecast.Cv2, forecast.NonZeroPeriods, forecast.Periods,
                PredictedMonthlyDemand: null, MonthlyRangeLow: null, MonthlyRangeHigh: null,
                leadTimeMonths, LeadTimeDemand: null, SafetyStock: null, ReorderPoint: null, OrderUpToLevel: null,
                available, RecommendedQuantity: null, StockingRangeLow: null, StockingRangeHigh: null,
                StockoutRisk: "Unknown", HoldingRisk: available > 0 ? "Review" : "None",
                forecast.Confidence, InsufficientData: true,
                Action: "Investigate — insufficient demand history", Rationale: insufficientRationale, forecast.Comparison);
        }

        var rate = forecast.RatePerPeriod;
        var leadPlusReview = leadTimeMonths + settings.ReviewPeriodMonths;

        // Per-period variability over the dense (zero-inclusive) series drives the protection-interval spread.
        var perPeriodStd = Statistics.Std(usageSeries);
        var sigmaLt = perPeriodStd * Math.Sqrt(leadPlusReview);

        var safety = ProcurementOptimiser.Z(settings.ServiceLevel) * sigmaLt;
        var leadTimeDemand = rate * leadTimeMonths;
        var reorderPoint = leadTimeDemand + safety;
        var orderUpTo = (rate * leadPlusReview) + safety;

        var point = LotCeil(orderUpTo - available);
        var low = LotCeil((rate * leadPlusReview) + (ProcurementOptimiser.Z(0.90) * sigmaLt) - available);
        var high = LotCeil((rate * leadPlusReview) + (ProcurementOptimiser.Z(0.99) * sigmaLt) - available);

        var stockoutRisk = available < leadTimeDemand ? "High"
            : available < reorderPoint ? "Medium"
            : "Low";

        var holdingRisk = orderUpTo > 0 && available > 2 * orderUpTo ? "Overstock" : "Healthy";

        var action = holdingRisk == "Overstock" ? "Reduce / trim stocking range"
            : stockoutRisk == "High" ? "Raise stocking level"
            : "Maintain current range";

        var rationale =
            $"{forecast.Class} demand (ADI {Fmt(forecast.Adi)}, CV² {Fmt(forecast.Cv2)}) forecast by {forecast.Method} at " +
            $"{Fmt(rate)} units/month. Over {Fmt(leadPlusReview, 1)} months (lead + review) at a {settings.ServiceLevel:P0} " +
            $"service level, safety stock is {Fmt(safety)} and the order-up-to level {Fmt(orderUpTo)}; netting {available} " +
            $"on-hand + inbound gives a recommended range of {low}–{high} units.";

        return new SparePartRecommendation(
            part.PartNumber, part.Name, part.Category, forecast.Class, forecast.Method,
            forecast.Adi, forecast.Cv2, forecast.NonZeroPeriods, forecast.Periods,
            rate, forecast.RangeLow, forecast.RangeHigh,
            leadTimeMonths, leadTimeDemand, safety, reorderPoint, orderUpTo,
            available, point, low, high, stockoutRisk, holdingRisk,
            forecast.Confidence, InsufficientData: false, action, rationale, forecast.Comparison);
    }

    /// <summary>
    /// Rolls superseded parts' historical usage forward onto the successor before forecasting: element-wise
    /// sum of equal-length dense monthly series (the successor first). Supersession chains resolve by
    /// pre-summing along the chain. Throws when lengths differ — callers align on a shared month axis.
    /// </summary>
    public static double[] RollUpUsage(IReadOnlyList<IReadOnlyList<double>> series)
    {
        if (series.Count == 0)
        {
            return [];
        }

        var length = series[0].Count;
        var rolled = new double[length];
        foreach (var s in series)
        {
            if (s.Count != length)
            {
                throw new ArgumentException("All usage series must share the same month axis to roll up.", nameof(series));
            }

            for (var i = 0; i < length; i++)
            {
                rolled[i] += s[i];
            }
        }

        return rolled;
    }

    private static int LotCeil(double quantity) => quantity <= 0 ? 0 : (int)Math.Ceiling(quantity);

    private static string Fmt(double value, int decimals = 2) => value.ToString($"F{decimals}", CultureInfo.InvariantCulture);
}
