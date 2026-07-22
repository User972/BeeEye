using BeeEye.Analytics.Configuration;
using BeeEye.Analytics.Demand;
using BeeEye.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BeeEye.Modules.SalesActuals.Application;

/// <summary>
/// UC3 read side. Loads sales at configuration grain plus current stock counts, and
/// runs the configuration-demand analysis.
/// </summary>
public sealed class ConfigurationReadService(BeeEyeDbContext db)
{
    public async Task<IReadOnlyList<ConfigDemandResult>> AnalyseAsync(ConfigDemandSettings settings, CancellationToken ct)
    {
        var sales = await LoadSalesAsync(ct);
        var stock = await LoadStockByConfigAsync(ct);
        return ConfigurationDemand.Analyse(sales, stock, settings);
    }

    public async Task<bool> HasDataAsync(CancellationToken ct) => await db.SalesFacts.AsNoTracking().AnyAsync(ct);

    private async Task<IReadOnlyList<SalesRow>> LoadSalesAsync(CancellationToken ct)
    {
        var rows = await db.SalesFacts.AsNoTracking()
            .Select(s => new { s.Location, s.Model, s.Variant, s.Colour, s.Interior, s.Year, s.Month, s.UnitsSold, s.DiscountApplied, s.DiscountPct })
            .ToListAsync(ct);

        return rows.Select(s => new SalesRow(
            s.Location, s.Model, s.Variant, $"{s.Year:D4}-{s.Month:D2}", s.UnitsSold, s.DiscountApplied, s.DiscountPct, s.Colour, s.Interior))
            .ToList();
    }

    private async Task<IReadOnlyDictionary<string, int>> LoadStockByConfigAsync(CancellationToken ct)
    {
        var items = await db.InventoryItems.AsNoTracking()
            .Select(i => new { i.Model, i.Variant, i.Colour, i.Interior })
            .ToListAsync(ct);

        return items
            .GroupBy(i => ConfigurationDemand.Key(i.Model, i.Variant, i.Colour, i.Interior))
            .ToDictionary(g => g.Key, g => g.Count());
    }
}
