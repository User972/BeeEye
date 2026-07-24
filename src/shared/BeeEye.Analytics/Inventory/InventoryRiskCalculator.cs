using BeeEye.Analytics.Demand;

namespace BeeEye.Analytics.Inventory;

/// <summary>
/// The explainable additive inventory aging-risk model (UC5), ported from engine.js
/// <c>computeInventory()</c>. Rankers and group-stock are computed over the full
/// inventory set; per-unit risk is therefore stable regardless of later filtering.
/// </summary>
public static class InventoryRiskCalculator
{
    public static IReadOnlyList<InventoryUnitRisk> Compute(
        IReadOnlyList<InventoryUnit> allUnits, IReadOnlyList<SalesRow> sales, RiskSettings settings)
    {
        var agg = DemandAggregates.Build(sales);
        var discount = DiscountResponses.Build(sales);
        var ad = settings.AnalysisDate;

        var holdRanker = Statistics.PercentileRanker(
            allUnits.Select(u => Math.Max(0, ad.DayNumber - u.DateOfPurchase.DayNumber) * (double)u.HoldingCostPerDay).ToArray());
        var leadRanker = Statistics.PercentileRanker(allUnits.Select(u => (double)u.LeadTimeDays).ToArray());

        var groupStock = new Dictionary<string, int>();
        foreach (var u in allUnits)
        {
            var key = $"{u.Location}|{u.Model}|{u.Variant}";
            groupStock[key] = (groupStock.TryGetValue(key, out var c) ? c : 0) + 1;
        }

        var invLocations = allUnits.Select(u => u.Location).Distinct().ToArray();
        var w = settings.Weights;
        var wsum = w.Cover + w.Aging + w.Demand + w.Holding + w.Lead;
        if (wsum == 0)
        {
            wsum = 100;
        }

        var coverMax = settings.CoverMax;
        var results = new List<InventoryUnitRisk>(allUnits.Count);

        foreach (var u in allUnits)
        {
            var invAge = ad.DayNumber - u.DateOfPurchase.DayNumber;
            var mfgAge = ad.DayNumber - u.DateOfManufacture.DayNumber;
            var accHold = Math.Max(0, invAge) * u.HoldingCostPerDay;
            var dem = DemandCalculator.Velocity(agg, u.Location, u.Model, u.Variant, settings.TrailingMonths);
            var trend = DemandCalculator.Trend(agg, u.Location, u.Model, u.Variant);
            var gStock = groupStock.TryGetValue($"{u.Location}|{u.Model}|{u.Variant}", out var gs) ? gs : 1;
            var cover = dem.Value > 0 ? gStock / dem.Value : gStock > 0 ? 999 : 0;

            var coverSub = dem.Value > 0 ? Statistics.Clamp((cover - 1) / (coverMax - 1) * 100, 0, 100) : 90;
            var agingSub = Statistics.Clamp((double)invAge / settings.AgingBands[3] * 100, 0, 100);
            var demandSub = trend.Direction == "declining"
                ? Statistics.Clamp(60 + Math.Min(40, Math.Abs(trend.ChangePct)), 0, 100)
                : trend.Direction == "stable" ? 35 : 10;
            var holdSub = holdRanker((double)accHold);
            var leadSub = leadRanker(u.LeadTimeDays);

            var factors = new List<RiskFactor>
            {
                new("cover", "estimated stock cover", w.Cover / 100 * coverSub,
                    dem.Value > 0 ? $"{cover:F1} months of cover" : "no reliable demand signal"),
                new("aging", "inventory holding age", w.Aging / 100 * agingSub, $"{invAge} days in stock"),
                new("demand", "recent demand trend", w.Demand / 100 * demandSub,
                    $"{trend.Direction} ({Format.SignPct(trend.ChangePct)} vs prior quarter)"),
                new("holding", "carrying-cost exposure", w.Holding / 100 * holdSub, $"{Format.Sar(accHold)} accrued"),
                new("lead", "historical lead time", w.Lead / 100 * leadSub, $"{u.LeadTimeDays} days"),
            };

            var score = (int)Statistics.Clamp(
                (int)Math.Round(factors.Sum(f => f.Points) * (100 / wsum), MidpointRounding.AwayFromZero), 0, 100);
            var band = Bands.Risk(score, settings.RiskBands);
            var recommendation = Recommend(u, invAge, cover, dem, trend, accHold, settings, agg, discount, groupStock, invLocations, coverMax);

            results.Add(new InventoryUnitRisk(
                u.StockId, u.ChassisNo, u.Brand, u.Model, u.Variant, u.Colour, u.Interior, u.Type, u.Location,
                u.DateOfPurchase, u.DateOfManufacture, u.ServiceDate, u.PurchasePrice, u.HoldingCostPerDay, u.LeadTimeDays,
                invAge, mfgAge, accHold, Bands.Aging(invAge, settings.AgingBands), Bands.Manufacturing(mfgAge),
                dem.Value, dem.Basis, dem.Confidence, dem.Detail, cover, gStock,
                trend.Direction, trend.ChangePct, score, band,
                factors.OrderByDescending(f => f.Points).ToList(), recommendation));
        }

        return results;
    }

    private static InventoryRecommendation Recommend(
        InventoryUnit u, int invAge, double cover, DemandVelocityResult dem, DemandTrendResult trend,
        decimal accHold, RiskSettings settings, DemandAggregates agg,
        IReadOnlyDictionary<string, DiscountResponse> discount, Dictionary<string, int> groupStock,
        string[] invLocations, double coverMax)
    {
        var mvResp = discount.TryGetValue($"{u.Model}|{u.Variant}", out var dr)
            ? dr
            : new DiscountResponse(false, 10, "0%", 0, 0);
        var criticalAge = settings.AgingBands[3];
        var watchAge = settings.AgingBands[2];

        if (dem.Value <= 0 && invAge > criticalAge)
        {
            return new InventoryRecommendation(
                "Prioritise liquidation", "Medium",
                "No reliable recent demand and the unit is beyond the critical aging threshold.",
                [$"{invAge} days in inventory", $"Demand basis: {dem.Basis}"],
                "Recovers capital and stops holding-cost accrual on a non-moving unit.",
                ["Assumes the absence of recent sales reflects genuinely weak demand, not a data gap."]);
        }

        if (dem.Value <= 0)
        {
            return new InventoryRecommendation(
                "Investigate demand data", "Low",
                "This location-model-variant has no reliable recent sales signal.",
                [$"Demand basis: {dem.Basis}", $"{invAge} days in inventory"],
                "Confirms whether the gap is a data issue or a true demand shortfall before acting.",
                ["service_date meaning is unconfirmed and excluded from risk."]);
        }

        var transfer = BestTransfer(agg, groupStock, invLocations, u.Model, u.Variant, u.Location, settings.TrailingMonths);
        if (cover > coverMax && transfer is { } t && t.DestCover < cover * 0.6)
        {
            return new InventoryRecommendation(
                "Transfer stock", t.Confidence,
                $"This location holds elevated cover while {t.Destination} shows stronger relative demand for the same model-variant.",
                [$"{cover:F1} months cover here", $"{t.Destination}: {t.DestCover:F1} months cover"],
                "Rebalances stock toward demand and lowers combined holding exposure.",
                ["Transfer feasibility and logistics cost not modelled in the POC."],
                Destination: t.Destination);
        }

        if (invAge > criticalAge && mvResp.Responsive)
        {
            return new InventoryRecommendation(
                "Apply controlled discount", "Medium",
                "Unit is beyond the critical aging band and this model-variant has historically moved more volume in discounted periods.",
                [$"{invAge} days in inventory", $"{Format.Sar(accHold)} holding cost accrued", $"Observed discount range: {mvResp.Range}"],
                "Accelerates sell-through within the historically observed discount range.",
                ["Association only — historical discounts are correlated with, not proven to cause, higher volume."],
                DiscountPct: mvResp.Suggest);
        }

        if (invAge > watchAge && trend.Direction != "increasing")
        {
            return new InventoryRecommendation(
                "Start targeted promotion", "Medium",
                "Inventory age is elevated and demand has softened but is not absent.",
                [$"{invAge} days in inventory", $"Demand trend: {trend.Direction}"],
                "Lifts visibility for a slowing unit before deeper discounting is needed.",
                ["Promotion response estimated from historical patterns."]);
        }

        if (cover > coverMax && trend.Direction != "increasing")
        {
            return new InventoryRecommendation(
                "Pause / reduce procurement", "Medium",
                "Stock cover is high and demand is flat or falling with no strong transfer destination.",
                [$"{cover:F1} months cover", $"Demand trend: {trend.Direction}"],
                "Prevents further build-up of an over-covered model-variant.",
                ["Assumes current demand pattern persists."]);
        }

        return new InventoryRecommendation(
            "Retain", "High",
            "Cover is within a healthy range, demand is holding and the unit is not aged.",
            [$"{cover:F1} months cover", $"{invAge} days in inventory", $"Demand trend: {trend.Direction}"],
            "No action required; continue to monitor.",
            []);
    }

    private static (string Destination, double DestCover, string Confidence)? BestTransfer(
        DemandAggregates agg, Dictionary<string, int> groupStock, string[] invLocations,
        string model, string variant, string srcLoc, int trailingMonths)
    {
        (string Destination, double DestCover, string Confidence)? best = null;
        foreach (var l in invLocations)
        {
            if (l == srcLoc)
            {
                continue;
            }

            var stock = groupStock.TryGetValue($"{l}|{model}|{variant}", out var c) ? c : 0;
            var d = DemandCalculator.Velocity(agg, l, model, variant, trailingMonths);
            if (d.Value <= 0)
            {
                continue;
            }

            var cover = stock / d.Value;
            if (best is null || cover < best.Value.DestCover)
            {
                best = (l, cover, d.Confidence);
            }
        }

        return best;
    }
}
