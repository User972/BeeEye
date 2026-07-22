using BeeEye.Analytics;
using BeeEye.Analytics.AfterSales;
using BeeEye.Modules.AfterSales.Contracts;
using BeeEye.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BeeEye.Modules.AfterSales.Application;

/// <summary>
/// UC6 read side. Loads synthetic service events + vehicle counts + monthly sales, maps them to the pure
/// <see cref="ServiceIntensity"/> analytics inputs, and returns the model-level intensity analysis. The
/// module only orchestrates — all statistics live in <c>BeeEye.Analytics</c>.
/// </summary>
public sealed class AfterSalesReadService(BeeEyeDbContext db)
{
    public async Task<bool> HasDataAsync(CancellationToken ct) => await db.ServiceEvents.AsNoTracking().AnyAsync(ct);

    public async Task<ServiceIntensityAnalysis> AnalyseAsync(CancellationToken ct)
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
