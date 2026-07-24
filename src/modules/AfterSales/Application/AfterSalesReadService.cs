using BeeEye.Analytics;
using BeeEye.Analytics.AfterSales;
using BeeEye.Modules.AfterSales.Contracts;
using BeeEye.Persistence;
using BeeEye.Persistence.Caching;
using Microsoft.EntityFrameworkCore;

namespace BeeEye.Modules.AfterSales.Application;

/// <summary>
/// UC6 read side. Loads synthetic service events + vehicle counts + monthly sales, maps them to the pure
/// <see cref="ServiceIntensity"/> analytics inputs, and returns the model-level intensity analysis. The
/// module only orchestrates — all statistics live in <c>BeeEye.Analytics</c>.
/// </summary>
/// <remarks>
/// The intensity analysis is a per-request recompute over the whole service/sales history (an O(models ×
/// months × maxLag) lagged correlation), and all four UC6 endpoints run it. It is now served through a
/// <see cref="DataVersionedCache"/> keyed on the current <see cref="DataVersion"/>, so the expensive load
/// and compute happen once per dataset version and a warm request is a cache read — see V3-PERF-001. The
/// analysis is a pure function of the (deterministic) data, which is exactly what makes the cache safe.
/// </remarks>
public sealed class AfterSalesReadService(BeeEyeDbContext db, DataVersionResolver dataVersion, DataVersionedCache cache)
{
    public async Task<bool> HasDataAsync(CancellationToken ct) => await db.ServiceEvents.AsNoTracking().AnyAsync(ct);

    /// <summary>
    /// The fleet intensity analysis for the current data version. A cache hit avoids both the DB load and
    /// the correlation compute; a single cached entry serves <c>/summary</c>, <c>/by-model</c> and
    /// <c>/model/{model}</c>, which each take their own slice of the same immutable result.
    /// </summary>
    public async Task<ServiceIntensityAnalysis> AnalyseAsync(CancellationToken ct)
    {
        var version = await dataVersion.CurrentAsync(ct);
        var key = ("after-sales:analysis", version.AnalysisDate, version.DatasetVersion);
        return await cache.GetOrComputeAsync(key, ComputeAnalysisAsync, ct);
    }

    private async Task<ServiceIntensityAnalysis> ComputeAnalysisAsync(CancellationToken ct)
    {
        var events = await db.ServiceEvents.AsNoTracking()
            .Select(e => new
            {
                e.Model, e.Variant, e.Location, e.ServiceDate, e.MonthsSinceSale, e.MileageBand, e.ServiceType, e.LaborHours, e.Vin,
            })
            .ToListAsync(ct);

        var records = events.Select(e => new ServiceRecord(
            e.Model, e.Variant, e.Location, MonthKey.Of(e.ServiceDate), e.MonthsSinceSale, e.MileageBand,
            ParseType(e.ServiceType), (double)e.LaborHours)).ToList();

        var vehiclesInOperation = await db.VehicleSales.AsNoTracking()
            .GroupBy(v => v.Model)
            .Select(g => new { Model = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var vioMap = vehiclesInOperation.ToDictionary(x => x.Model, x => x.Count, StringComparer.Ordinal);

        var vehiclesWithEvents = events
            .GroupBy(e => e.Model, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Vin).Distinct(StringComparer.Ordinal).Count(), StringComparer.Ordinal);

        var sales = await db.SalesFacts.AsNoTracking()
            .Select(s => new { s.Model, s.Year, s.Month, s.UnitsSold })
            .ToListAsync(ct);
        var monthlySales = sales
            .Select(s => new MonthlyVolume(s.Model, $"{s.Year:D4}-{s.Month:D2}", s.UnitsSold))
            .ToList();

        return ServiceIntensity.Analyse(records, vioMap, vehiclesWithEvents, monthlySales, ServiceIntensitySettings.Default);
    }

    public async Task<AfterSalesFilterOptions> FilterOptionsAsync(CancellationToken ct)
    {
        var dims = await db.ServiceEvents.AsNoTracking()
            .Select(e => new { e.Model, e.Variant, e.Location, e.MileageBand })
            .ToListAsync(ct);

        return new AfterSalesFilterOptions(
            dims.Select(d => d.Model).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList(),
            dims.Select(d => d.Variant).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList(),
            dims.Select(d => d.Location).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList(),
            dims.Select(d => d.MileageBand).Distinct()
                .OrderBy(b => AfterSalesBands.OrderIndex(AfterSalesBands.MileageOrder, b)).ThenBy(b => b, StringComparer.Ordinal).ToList(),
            Enum.GetValues<ServiceType>().Select(t => t.ToString()).ToList());
    }

    private static ServiceType ParseType(string value) => value switch
    {
        "Routine" => ServiceType.Routine,
        "Repair" => ServiceType.Repair,
        "Warranty" => ServiceType.Warranty,
        "Recall" => ServiceType.Recall,
        _ => ServiceType.Repair,
    };
}
