using BeeEye.Analytics.Demand;
using BeeEye.Analytics.Inventory;
using BeeEye.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BeeEye.Modules.Inventory.Application;

/// <summary>
/// Loads inventory + sales from the operational store, maps them to analytics inputs,
/// and runs the explainable UC5 risk model. Risk is computed over the full inventory
/// set so per-unit scores are stable regardless of any later filtering.
/// </summary>
public sealed class InventoryReadService(BeeEyeDbContext db)
{
    public async Task<IReadOnlyList<InventoryUnitRisk>> ComputeAsync(RiskSettings settings, CancellationToken ct)
    {
        var items = await db.InventoryItems.AsNoTracking().ToListAsync(ct);
        var units = items.Select(i => new InventoryUnit(
            i.StockId, i.ChassisNo, i.Brand, i.Model, i.Variant, i.Colour, i.Interior, i.Type, i.Location,
            i.DateOfPurchase, i.DateOfManufacture, i.ServiceDate, i.PurchasePrice, i.HoldingCostPerDay, i.LeadTimeDays))
            .ToList();

        var sales = await LoadSalesAsync(ct);
        return InventoryRiskCalculator.Compute(units, sales, settings);
    }

    private async Task<IReadOnlyList<SalesRow>> LoadSalesAsync(CancellationToken ct)
    {
        var rows = await db.SalesFacts.AsNoTracking()
            .Select(s => new
            {
                s.Location,
                s.Model,
                s.Variant,
                s.Year,
                s.Month,
                s.UnitsSold,
                s.DiscountApplied,
                s.DiscountPct,
            })
            .ToListAsync(ct);

        return rows.Select(s => new SalesRow(
            s.Location, s.Model, s.Variant, $"{s.Year:D4}-{s.Month:D2}", s.UnitsSold, s.DiscountApplied, s.DiscountPct))
            .ToList();
    }

    public async Task<bool> HasDataAsync(CancellationToken ct)
        => await db.InventoryItems.AsNoTracking().AnyAsync(ct);

    /// <summary>
    /// Latest observed data date — the default analysis date when the caller supplies none.
    /// The API contract forbids a silent wall-clock "now" (docs/architecture/api-design.md);
    /// anchoring on the data keeps a frozen dataset reproducible while advancing with
    /// ingestion, so newly ingested stock can never have a negative age.
    /// </summary>
    public async Task<DateOnly?> LatestDataDateAsync(CancellationToken ct)
        => await db.InventoryItems.AsNoTracking().MaxAsync(i => (DateOnly?)i.DateOfPurchase, ct);
}
