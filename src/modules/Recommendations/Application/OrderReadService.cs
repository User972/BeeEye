using BeeEye.Analytics;
using BeeEye.Analytics.Forecasting;
using BeeEye.Analytics.Optimisation;
using BeeEye.Modules.Recommendations.Contracts;
using BeeEye.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BeeEye.Modules.Recommendations.Application;

/// <summary>
/// UC1 read side. Separates the demand forecast (via BeeEye.Analytics Forecaster) from
/// the business constraints (via OrderOptimiser), so an unconstrained forecast is never
/// presented as an order. Ordering grain is model·variant (national).
/// </summary>
public sealed class OrderReadService(BeeEyeDbContext db)
{
    private sealed record Row(string Model, string Variant, string MonthKey, double Units);

    public async Task<IReadOnlyList<OrderRecommendationRow>> RecommendAsync(OrderScenario scenario, CancellationToken ct)
    {
        var (rows, months) = await LoadAsync(ct);
        if (months.Count < 3)
        {
            return [];
        }

        var stock = await StockByConfigAsync(ct);
        var options = new ForecastOptions(Horizon: scenario.Horizon, Holdout: 6);
        var results = new List<OrderRecommendationRow>();

        foreach (var group in rows.GroupBy(r => (r.Model, r.Variant)))
        {
            var groupRows = group.ToList();
            var window = ActiveMonths(groupRows, months);
            var series = BuildSeries(groupRows, window);
            var forecast = Forecaster.Run(series, window, options);
            var monthlyVelocity = Statistics.Mean(series.Length >= 3 ? series[^3..] : series);
            var current = stock.TryGetValue($"{group.Key.Model}|{group.Key.Variant}", out var s) ? s : 0;

            var constraints = new OrderConstraints
            {
                CurrentInventory = current,
                InboundInventory = scenario.Inbound,
                ConfirmedOrders = scenario.ConfirmedOrders,
                MinOrderQuantity = scenario.MinOrderQuantity,
                OrderMultiple = scenario.OrderMultiple,
                TargetCoverMonths = scenario.TargetCoverMonths,
                AllocationLimit = scenario.AllocationLimit,
            };

            var recommendation = OrderOptimiser.Recommend(
                forecast.FutureSum, monthlyVelocity, constraints, Confidence(forecast.Accuracy.Wmape));

            results.Add(OrderRecommendationRow.From(
                group.Key.Model, group.Key.Variant, forecast.ChosenName, forecast.Accuracy.Wmape, monthlyVelocity, recommendation));
        }

        return results.OrderByDescending(r => r.RecommendedQuantity).ThenBy(r => r.Model, StringComparer.Ordinal).ToList();
    }

    public async Task<bool> HasDataAsync(CancellationToken ct) => await db.SalesFacts.AsNoTracking().AnyAsync(ct);

    /// <summary>Distinct model/variant dimension values, without running the per-config forecast.</summary>
    public async Task<(IReadOnlyList<string> Models, IReadOnlyList<string> Variants)> FilterOptionsAsync(CancellationToken ct)
    {
        var (rows, _) = await LoadAsync(ct);
        return (
            rows.Select(r => r.Model).Distinct().OrderBy(x => x).ToList(),
            rows.Select(r => r.Variant).Distinct().OrderBy(x => x).ToList());
    }

    /// <summary>
    /// Observed average selling price per model · variant, aggregated in the database rather than by
    /// materialising sales history, and weighted by units so high-volume months dominate — the same
    /// basis the UC1 screen values an order at.
    /// </summary>
    public async Task<IReadOnlyDictionary<(string Model, string Variant), decimal>> AverageSellingPricesAsync(
        CancellationToken ct)
    {
        var rows = await db.SalesFacts.AsNoTracking()
            .Where(f => f.UnitsSold > 0)
            .GroupBy(f => new { f.Model, f.Variant })
            .Select(g => new
            {
                g.Key.Model,
                g.Key.Variant,
                Revenue = g.Sum(f => f.Revenue),
                Units = g.Sum(f => f.UnitsSold),
            })
            .ToListAsync(ct);

        return rows
            .Where(r => r.Units > 0)
            .ToDictionary(r => (r.Model, r.Variant), r => r.Revenue / r.Units);
    }

    private static string Confidence(double? wmape) => wmape is null ? "Low" : wmape < 15 ? "High" : wmape < 30 ? "Medium" : "Low";

    /// <summary>
    /// A configuration's demand window starts at its first-ever sale: earlier months on the
    /// global axis are not real zero-demand observations (the configuration did not exist yet)
    /// and would train the forecaster toward zero. A minimum of three months is kept so the
    /// forecaster always has enough points to back-test.
    /// </summary>
    private static IReadOnlyList<string> ActiveMonths(IReadOnlyList<Row> rows, IReadOnlyList<string> months)
    {
        var first = rows.Min(r => r.MonthKey)!;
        var start = 0;
        while (start < months.Count && string.CompareOrdinal(months[start], first) < 0)
        {
            start++;
        }

        start = Math.Min(start, Math.Max(0, months.Count - 3));
        return start == 0 ? months : months.Skip(start).ToList();
    }

    private static double[] BuildSeries(IReadOnlyList<Row> rows, IReadOnlyList<string> months)
    {
        var byMonth = new Dictionary<string, double>();
        foreach (var r in rows)
        {
            byMonth[r.MonthKey] = (byMonth.TryGetValue(r.MonthKey, out var v) ? v : 0) + r.Units;
        }

        return months.Select(m => byMonth.TryGetValue(m, out var v) ? v : 0).ToArray();
    }

    private async Task<(List<Row> Rows, IReadOnlyList<string> Months)> LoadAsync(CancellationToken ct)
    {
        var raw = await db.SalesFacts.AsNoTracking()
            .Select(s => new { s.Model, s.Variant, s.Year, s.Month, s.UnitsSold })
            .ToListAsync(ct);

        var rows = raw.Select(s => new Row(s.Model, s.Variant, $"{s.Year:D4}-{s.Month:D2}", s.UnitsSold)).ToList();
        if (rows.Count == 0)
        {
            return (rows, []);
        }

        // rows is non-empty here (guarded above), so Min/Max are never null.
        return (rows, MonthKey.Range(rows.Min(r => r.MonthKey)!, rows.Max(r => r.MonthKey)!));
    }

    private async Task<IReadOnlyDictionary<string, int>> StockByConfigAsync(CancellationToken ct)
    {
        var items = await db.InventoryItems.AsNoTracking().Select(i => new { i.Model, i.Variant }).ToListAsync(ct);
        return items.GroupBy(i => $"{i.Model}|{i.Variant}").ToDictionary(g => g.Key, g => g.Count());
    }
}
