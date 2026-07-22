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
            var series = BuildSeries(group.ToList(), months);
            var forecast = Forecaster.Run(series, months, options);
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

    private static string Confidence(double? wmape) => wmape is null ? "Low" : wmape < 15 ? "High" : wmape < 30 ? "Medium" : "Low";

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

        return (rows, MonthKey.Range(rows.Min(r => r.MonthKey), rows.Max(r => r.MonthKey)));
    }

    private async Task<IReadOnlyDictionary<string, int>> StockByConfigAsync(CancellationToken ct)
    {
        var items = await db.InventoryItems.AsNoTracking().Select(i => new { i.Model, i.Variant }).ToListAsync(ct);
        return items.GroupBy(i => $"{i.Model}|{i.Variant}").ToDictionary(g => g.Key, g => g.Count());
    }
}
