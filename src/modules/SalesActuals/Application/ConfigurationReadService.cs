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

    /// <summary>Distinct dimension values, without running the full demand analysis.</summary>
    public async Task<(IReadOnlyList<string> Models, IReadOnlyList<string> Variants, IReadOnlyList<string> Colours, IReadOnlyList<string> Interiors)> FilterOptionsAsync(CancellationToken ct)
    {
        var sales = await LoadSalesAsync(ct);
        IReadOnlyList<string> SortedDistinct(Func<SalesRow, string> selector)
            => sales.Select(selector).Distinct().OrderBy(x => x).ToList();

        return (SortedDistinct(s => s.Model), SortedDistinct(s => s.Variant), SortedDistinct(s => s.Colour), SortedDistinct(s => s.Interior));
    }

    /// <summary>
    /// Units-weighted average selling price per model · variant, aggregated in the database rather
    /// than by materialising sales history. Weighting by units means high-volume months dominate,
    /// which is the same basis the UC3 screen values stock at.
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
