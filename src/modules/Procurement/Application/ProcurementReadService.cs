using BeeEye.Analytics;
using BeeEye.Analytics.Optimisation;
using BeeEye.Modules.Procurement.Contracts;
using BeeEye.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BeeEye.Modules.Procurement.Application;

/// <summary>
/// UC4 read side. Derives monthly demand mean/variability from sales and the lead-time
/// mean/variability from the observed inventory lead times, then runs the procurement
/// optimiser. Inbound inventory is always netted off.
/// </summary>
public sealed class ProcurementReadService(BeeEyeDbContext db)
{
    private const double DaysPerMonth = 30.44;

    private sealed record SalesRow(string Model, string Variant, string MonthKey, double Units);
    private sealed record LeadAgg(int Count, double AvgLeadDays, double StdLeadDays);

    public async Task<IReadOnlyList<ProcurementRow>> RecommendAsync(ProcurementScenario scenario, CancellationToken ct)
    {
        var (rows, months) = await LoadSalesAsync(ct);
        if (months.Count == 0)
        {
            return [];
        }

        var inventory = await LeadTimeAggregatesAsync(ct);
        var results = new List<ProcurementRow>();

        foreach (var group in rows.GroupBy(r => (r.Model, r.Variant)))
        {
            var groupRows = group.ToList();
            var window = ActiveMonths(groupRows, months);
            var series = BuildSeries(groupRows, window);
            var mean = Statistics.Mean(series);
            var std = Statistics.Std(series);
            var key = $"{group.Key.Model}|{group.Key.Variant}";
            inventory.TryGetValue(key, out var agg);

            var leadMonths = scenario.LeadTimeMonthsOverride
                ?? (agg is { AvgLeadDays: > 0 } ? agg.AvgLeadDays / DaysPerMonth : 2.0);
            var leadStdMonths = agg is { StdLeadDays: > 0 } ? agg.StdLeadDays / DaysPerMonth : 0.5;
            var current = agg?.Count ?? 0;

            var settings = new ProcurementSettings
            {
                ServiceLevel = scenario.ServiceLevel,
                LeadTimeMonths = leadMonths,
                LeadTimeStdMonths = leadStdMonths,
                ReviewPeriodMonths = scenario.ReviewPeriodMonths,
                MinOrderQuantity = scenario.MinOrderQuantity,
                OrderMultiple = scenario.OrderMultiple,
            };

            var confidence = mean > 0
                ? std / mean < 0.5 ? "High" : std / mean < 1 ? "Medium" : "Low"
                : "Low";

            var recommendation = ProcurementOptimiser.Recommend(mean, std, current, scenario.Inbound, settings, confidence);
            results.Add(ProcurementRow.From(group.Key.Model, group.Key.Variant, leadMonths, recommendation));
        }

        return results.OrderByDescending(r => r.RecommendedQuantity).ThenBy(r => r.Model, StringComparer.Ordinal).ToList();
    }

    public async Task<bool> HasDataAsync(CancellationToken ct) => await db.SalesFacts.AsNoTracking().AnyAsync(ct);

    /// <summary>Distinct model/variant dimension values, without running the procurement optimiser.</summary>
    public async Task<(IReadOnlyList<string> Models, IReadOnlyList<string> Variants)> FilterOptionsAsync(CancellationToken ct)
    {
        var (rows, _) = await LoadSalesAsync(ct);
        return (
            rows.Select(r => r.Model).Distinct().OrderBy(x => x).ToList(),
            rows.Select(r => r.Variant).Distinct().OrderBy(x => x).ToList());
    }

    /// <summary>
    /// A configuration's demand window starts at its first-ever sale: earlier months on the
    /// global axis are not real zero-demand observations (the configuration did not exist yet)
    /// and would deflate the demand mean and corrupt the reorder policy. A minimum of three
    /// months is kept so variability stays measurable for very new configurations.
    /// </summary>
    private static IReadOnlyList<string> ActiveMonths(IReadOnlyList<SalesRow> rows, IReadOnlyList<string> months)
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

    private static double[] BuildSeries(IReadOnlyList<SalesRow> rows, IReadOnlyList<string> months)
    {
        var byMonth = new Dictionary<string, double>();
        foreach (var r in rows)
        {
            byMonth[r.MonthKey] = (byMonth.TryGetValue(r.MonthKey, out var v) ? v : 0) + r.Units;
        }

        return months.Select(m => byMonth.TryGetValue(m, out var v) ? v : 0).ToArray();
    }

    private async Task<(List<SalesRow> Rows, IReadOnlyList<string> Months)> LoadSalesAsync(CancellationToken ct)
    {
        var raw = await db.SalesFacts.AsNoTracking()
            .Select(s => new { s.Model, s.Variant, s.Year, s.Month, s.UnitsSold })
            .ToListAsync(ct);

        var rows = raw.Select(s => new SalesRow(s.Model, s.Variant, $"{s.Year:D4}-{s.Month:D2}", s.UnitsSold)).ToList();
        if (rows.Count == 0)
        {
            return (rows, []);
        }

        // rows is non-empty here (guarded above), so Min/Max are never null.
        return (rows, MonthKey.Range(rows.Min(r => r.MonthKey)!, rows.Max(r => r.MonthKey)!));
    }

    private async Task<IReadOnlyDictionary<string, LeadAgg>> LeadTimeAggregatesAsync(CancellationToken ct)
    {
        var items = await db.InventoryItems.AsNoTracking()
            .Select(i => new { i.Model, i.Variant, i.LeadTimeDays })
            .ToListAsync(ct);

        return items
            .GroupBy(i => $"{i.Model}|{i.Variant}")
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var leads = g.Select(x => (double)x.LeadTimeDays).ToList();
                    return new LeadAgg(g.Count(), Statistics.Mean(leads), Statistics.Std(leads));
                });
    }
}
