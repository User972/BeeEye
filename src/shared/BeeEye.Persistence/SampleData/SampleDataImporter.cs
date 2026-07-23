using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BeeEye.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BeeEye.Persistence.SampleData;

/// <summary>
/// Loads the embedded sample datasets into PostgreSQL for dev seeding and integration
/// tests. Idempotent: batch identity is (object, file-checksum), so re-running an
/// unchanged file is skipped, and a changed file *replaces* the prior batch for that
/// object (supersede) instead of accumulating alongside it — read services query the
/// fact tables with no batch filter, so stale batches would double every aggregate.
/// Real Oracle Fusion ingestion is the Integration module's concern.
/// </summary>
public sealed class SampleDataImporter(BeeEyeDbContext db)
{
    private const string SourceSystem = "wireframe-sample";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Imports both sample objects in a single transaction, so a failure in the second
    /// object never leaves the database half-updated (e.g. new sales with stale inventory).
    /// Assumes the schema already exists.
    /// </summary>
    public async Task<ImportResult> ImportAsync(CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var sales = await ImportSalesAsync(ct);
        var inventory = await ImportInventoryAsync(ct);
        await tx.CommitAsync(ct);
        return new ImportResult(sales, inventory);
    }

    public async Task<ImportObjectResult> ImportSalesAsync(CancellationToken ct = default)
    {
        var (bytes, checksum) = ReadResource("sales.json");
        var existing = await FindBatchAsync("sales", checksum, ct);
        if (existing is not null)
        {
            return new ImportObjectResult("sales", "skipped", 0, existing.RecordCount, checksum);
        }

        var records = JsonSerializer.Deserialize<List<SalesRecordDto>>(bytes, JsonOptions) ?? [];
        var batchId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var facts = new List<SalesFact>(records.Count);
        for (var i = 0; i < records.Count; i++)
        {
            var r = records[i];
            facts.Add(new SalesFact
            {
                Id = Guid.NewGuid(),
                SaleMonth = ParseDate(r.SaleDate),
                Year = r.Year,
                Month = r.Month,
                Location = r.Location,
                Model = r.Model,
                Variant = r.Variant,
                Colour = r.Colour,
                Interior = r.Interior,
                Brand = r.Brand,
                Type = r.Type,
                UnitsSold = r.UnitsSold,
                UnitPrice = r.UnitPrice,
                Revenue = r.Revenue,
                Currency = r.Currency,
                DiscountApplied = Truthy(r.DiscountApplied) || r.DiscountPct > 0,
                DiscountPct = r.DiscountPct,
                IsRamadan = Truthy(r.IsRamadan),
                DateOfManufacture = ParseDate(r.DateOfManufacture),
                // Deterministic content hash (SalesFact.RowHash contract): identical row
                // content always hashes the same across imports, so the unique index can
                // reject cross-import duplicates. The row index disambiguates genuinely
                // identical rows within one extract without breaking determinism.
                RowHash = Sha256($"{i}|{r.SaleDate}|{r.Location}|{r.Model}|{r.Variant}|{r.Colour}|{r.Interior}|{r.UnitsSold}|{r.UnitPrice}|{r.Revenue}|{r.DiscountPct}|{r.IsRamadan}"),
                IngestionBatchId = batchId,
                IngestedAtUtc = now,
            });
        }

        await using var ownTx = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        await SupersedePriorBatchesAsync("sales", ct);
        await PersistBatchAsync(batchId, "sales", checksum, "sales.json", facts, ct);
        if (ownTx is not null)
        {
            await ownTx.CommitAsync(ct);
        }

        return new ImportObjectResult("sales", "imported", facts.Count, records.Count, checksum);
    }

    public async Task<ImportObjectResult> ImportInventoryAsync(CancellationToken ct = default)
    {
        var (bytes, checksum) = ReadResource("inventory.json");
        var existing = await FindBatchAsync("inventory", checksum, ct);
        if (existing is not null)
        {
            return new ImportObjectResult("inventory", "skipped", 0, existing.RecordCount, checksum);
        }

        var records = JsonSerializer.Deserialize<List<InventoryRecordDto>>(bytes, JsonOptions) ?? [];
        var batchId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var items = records.Select(r => new InventoryItem
        {
            Id = Guid.NewGuid(),
            StockId = r.StockId,
            ChassisNo = r.ChassisNo,
            Model = r.Model,
            Variant = r.Variant,
            Colour = r.Colour,
            Interior = r.Interior,
            Brand = r.Brand,
            Type = r.Type,
            Location = r.Location,
            DateOfPurchase = ParseDate(r.DateOfPurchase),
            DateOfManufacture = ParseDate(r.DateOfManufacture),
            ServiceDate = string.IsNullOrWhiteSpace(r.ServiceDate) ? null : ParseDate(r.ServiceDate),
            LeadTimeDays = r.LeadTimeDays,
            PurchasePrice = r.PurchasePrice,
            HoldingCostPerDay = r.HoldingCostPerDay,
            Currency = r.Currency,
            IngestionBatchId = batchId,
            IngestedAtUtc = now,
        }).ToList();

        await using var ownTx = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        await SupersedePriorBatchesAsync("inventory", ct);
        await PersistBatchAsync(batchId, "inventory", checksum, "inventory.json", items, ct);
        if (ownTx is not null)
        {
            await ownTx.CommitAsync(ct);
        }

        return new ImportObjectResult("inventory", "imported", items.Count, records.Count, checksum);
    }

    private async Task<IngestionBatch?> FindBatchAsync(string obj, string checksum, CancellationToken ct)
        => await db.IngestionBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SourceObject == obj && x.Checksum == checksum && x.Status == "completed", ct);

    /// <summary>
    /// A changed sample file replaces the prior data for its object: earlier completed
    /// batches are marked superseded and their rows deleted, so aggregates never
    /// double-count two generations of the same dataset.
    /// </summary>
    private async Task SupersedePriorBatchesAsync(string obj, CancellationToken ct)
    {
        var priorIds = await db.IngestionBatches
            .Where(b => b.SourceSystem == SourceSystem && b.SourceObject == obj && b.Status == "completed")
            .Select(b => b.Id)
            .ToListAsync(ct);
        if (priorIds.Count == 0)
        {
            return;
        }

        if (obj == "sales")
        {
            await db.SalesFacts.Where(f => priorIds.Contains(f.IngestionBatchId)).ExecuteDeleteAsync(ct);
        }
        else
        {
            await db.InventoryItems.Where(i => priorIds.Contains(i.IngestionBatchId)).ExecuteDeleteAsync(ct);
        }

        await db.IngestionBatches
            .Where(b => priorIds.Contains(b.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, "superseded"), ct);
    }

    private async Task PersistBatchAsync<T>(
        Guid batchId, string obj, string checksum, string fileName, List<T> entities, CancellationToken ct)
        where T : class
    {
        var start = DateTimeOffset.UtcNow;
        db.IngestionBatches.Add(new IngestionBatch
        {
            Id = batchId,
            SourceSystem = SourceSystem,
            SourceObject = obj,
            Checksum = checksum,
            FileName = fileName,
            RecordCount = entities.Count,
            Status = "completed",
            StartedAtUtc = start,
            CompletedAtUtc = DateTimeOffset.UtcNow,
        });
        db.AddRange(entities);
        await db.SaveChangesAsync(ct);
    }

    private static (byte[] Bytes, string Checksum) ReadResource(string name)
    {
        var resource = $"BeeEye.Persistence.SampleData.{name}";
        using var stream = typeof(SampleDataImporter).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded sample data '{resource}' not found.");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();
        return (bytes, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    private static DateOnly ParseDate(string value)
        => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static bool Truthy(string value)
    {
        var s = value.Trim().ToLowerInvariant();
        return s is "true" or "yes" or "1";
    }

    private static string Sha256(string input)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
}
