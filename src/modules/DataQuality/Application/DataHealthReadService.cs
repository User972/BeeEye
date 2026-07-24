using System.Globalization;
using BeeEye.Analytics.DataQuality;
using BeeEye.Modules.DataQuality.Contracts;
using BeeEye.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BeeEye.Modules.DataQuality.Application;

/// <summary>
/// Assembles the Data Health view (V3-GOV-008): it counts the real sales and inventory rows in the
/// operational store, runs the <see cref="DataQualityCalculator"/> over them, derives the coverage
/// labels, and composes the seven governed data sources with an honest real/demo/blocked status.
/// <para>
/// The two real sources report measured counts and derived coverage; the four synthetic sources and the
/// one blocked source are declared honestly — a synthetic count is never presented as a measured one,
/// which is the entire point of a transparency screen.
/// </para>
/// </summary>
public sealed class DataHealthReadService(BeeEyeDbContext db)
{
    // The status word a source carries, and the kind that drives its word+icon+colour chip.
    private const string KindReady = "ready";
    private const string KindAssumptions = "assumptions";
    private const string KindDemo = "demo";
    private const string KindBlocked = "blocked";

    public async Task<DataHealthResponse> ComputeAsync(DateTimeOffset generatedAtUtc, CancellationToken ct)
    {
        // Project to anonymous rows on the server, then map in memory — the same shape the UC5 read
        // service uses, so the query stays trivially translatable.
        var salesRaw = await db.SalesFacts.AsNoTracking()
            .Select(s => new { s.Location, s.Model, s.UnitsSold, s.UnitPrice, s.Revenue, s.DiscountPct })
            .ToListAsync(ct);

        var invRaw = await db.InventoryItems.AsNoTracking()
            .Select(i => new
            {
                i.StockId,
                i.ChassisNo,
                i.Location,
                i.PurchasePrice,
                i.HoldingCostPerDay,
                i.DateOfPurchase,
                i.DateOfManufacture,
                i.LeadTimeDays,
            })
            .ToListAsync(ct);

        var sales = salesRaw
            .Select(s => new DataQualitySalesRow(s.Location, s.UnitsSold, s.UnitPrice, s.Revenue, s.DiscountPct))
            .ToList();
        var inventory = invRaw
            .Select(i => new DataQualityInventoryRow(
                i.StockId, i.ChassisNo, i.Location, i.PurchasePrice, i.HoldingCostPerDay,
                i.DateOfPurchase, i.DateOfManufacture, i.LeadTimeDays))
            .ToList();

        var report = DataQualityCalculator.Compute(sales, inventory);

        // Coverage and dimension counts come straight from the data, never from a hard-coded literal.
        // Min/Max over the (nullable) projection so an empty table yields null rather than throwing.
        DateOnly? minMonth = null, maxMonth = null;
        if (salesRaw.Count > 0)
        {
            minMonth = await db.SalesFacts.AsNoTracking().MinAsync(s => (DateOnly?)s.SaleMonth, ct);
            maxMonth = await db.SalesFacts.AsNoTracking().MaxAsync(s => (DateOnly?)s.SaleMonth, ct);
        }

        var coverage = FormatMonthRange(minMonth, maxMonth);
        var models = salesRaw.Select(s => s.Model).Distinct(StringComparer.Ordinal).Count();
        var locations = salesRaw.Select(s => s.Location).Distinct(StringComparer.Ordinal).Count();

        var snapshot = invRaw.Count == 0
            ? (DateOnly?)null
            : await db.InventoryItems.AsNoTracking().MaxAsync(i => (DateOnly?)i.DateOfPurchase, ct);
        var invCoverage = snapshot is null ? "—" : "Snapshot @ " + FormatMonth(snapshot.Value);

        var sources = ComposeSources(report.SalesRows, report.InvRows, coverage, invCoverage, report.Mismatch);
        var issues = report.Issues
            .Select(i => new DataQualityIssueDto(i.Id, i.Label, i.Count, i.Severity, i.Note))
            .ToList();

        return new DataHealthResponse(
            report.Score,
            report.ScoreBand,
            report.SalesRows,
            report.InvRows,
            coverage,
            models,
            locations,
            sources,
            issues,
            generatedAtUtc);
    }

    /// <summary>
    /// Composes the seven governed data sources from the measured facts. Pure and side-effect-free so
    /// the status derivation (Ready ↔ Ready-with-assumptions), the demo rows and the blocked row are all
    /// unit-testable without a database.
    /// </summary>
    public static IReadOnlyList<DataSourceDto> ComposeSources(
        int salesRows,
        int invRows,
        string salesCoverage,
        string invCoverage,
        IReadOnlyList<string> mismatch)
    {
        var invReady = mismatch.Count == 0;

        return
        [
            new DataSourceDto(
                "Sales history", "Fusion Order Management", "Ready", KindReady,
                salesRows.ToString(CultureInfo.InvariantCulture), salesCoverage,
                "Supplied workbook — parsed & validated."),

            new DataSourceDto(
                "Inventory on-hand", "Fusion Inventory Management",
                invReady ? "Ready" : "Ready with assumptions", invReady ? KindReady : KindAssumptions,
                invRows.ToString(CultureInfo.InvariantCulture), invCoverage,
                invReady
                    ? "Supplied workbook — parsed & validated."
                    : string.Join(", ", mismatch) + " sell but hold no inventory snapshot."),

            new DataSourceDto(
                "Supplier master & PO history", "Fusion Procurement", "Demo data", KindDemo,
                "Synthetic", "Trailing 18 months (synthetic)",
                "Not supplied — deterministic synthetic fixture for the prototype."),

            new DataSourceDto(
                "Service / repair-order history", "Fusion Service", "Demo data", KindDemo,
                "Synthetic", "Illustrative cohorts",
                "Not supplied — illustrative after-sales fixture."),

            new DataSourceDto(
                "Parts usage & parts inventory", "Fusion Inventory / Service", "Demo data", KindDemo,
                "Synthetic", "Illustrative",
                "Not supplied — synthetic parts fixture linked to sales mix."),

            new DataSourceDto(
                "Vehicle mileage & warranty claims", "Fusion Service / CRM", "Blocked", KindBlocked,
                "0", "—",
                "Not available in sample; required for production after-sales modelling."),

            new DataSourceDto(
                "Open purchase orders / inbound", "Fusion Procurement", "Demo data", KindDemo,
                "Synthetic", "Synthetic inbound",
                "Not supplied — inbound modelled from synthetic PO fixture."),
        ];
    }

    private static string FormatMonthRange(DateOnly? first, DateOnly? last)
    {
        if (first is null || last is null)
        {
            return "—";
        }

        var f = FormatMonth(first.Value);
        var l = FormatMonth(last.Value);
        return f == l ? f : $"{f} → {l}";
    }

    private static string FormatMonth(DateOnly month) =>
        month.ToString("MMM yyyy", CultureInfo.InvariantCulture);
}
