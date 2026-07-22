using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BeeEye.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BeeEye.Persistence.SyntheticData;

/// <summary>Configurable generation parameters. Defaults target the full vehicle population.</summary>
public sealed record SyntheticGenerationSettings
{
    /// <summary>Global PRNG seed — same seed ⇒ identical dataset.</summary>
    public ulong Seed { get; init; } = 0xBEE_5EED_1234_5678UL;

    /// <summary>Fraction of each cell's units expanded into vehicles (1.0 = the full ~24,130). Bounded to (0,1].</summary>
    public double Density { get; init; } = 1.0;

    public int WarrantyMonths { get; init; } = 36;
    public int WarrantyKm { get; init; } = 100_000;
    public double WarrantyMonthlyProbability { get; init; } = 0.012;
    public double RepairBaseProbability { get; init; } = 0.012;

    public static SyntheticGenerationSettings Default => new();
}

/// <summary>Outcome of a synthetic-data import.</summary>
public sealed record SyntheticImportResult(
    string Status, int Vehicles, int ServiceEvents, int Parts, int PartUsages, string Checksum);

/// <summary>
/// Deterministically derives a plausible after-sales &amp; spare-parts dataset from the real sales facts,
/// so UC6/UC7 correlations are meaningful and reproducible. Marked <b>synthetic-demo</b> throughout —
/// never presented as real Oracle Fusion data. Idempotent like <c>SampleDataImporter</c>: batch identity
/// is (object, checksum) where the checksum derives from the seed + input shape, so a re-run is skipped.
/// </summary>
public sealed class SyntheticAfterSalesImporter(BeeEyeDbContext db)
{
    public const string SourceSystem = "synthetic-demo";
    private const string SourceObject = "after-sales-parts";

    /// <summary>Bump when the generation logic changes so existing synthetic data is regenerated.</summary>
    private const string CatalogVersion = "v4";
    private const int InsertChunk = 10_000;

    private sealed record SalesRow(string Model, string Variant, string Location, string Colour, int Year, int Month, int UnitsSold);

    private static readonly Dictionary<string, int> ModelBaseKm = new(StringComparer.Ordinal)
    {
        [PartsCatalog.HavalH9] = 1700,
        [PartsCatalog.Patrol] = 1650,
        [PartsCatalog.Corolla] = 1400,
        [PartsCatalog.Camry] = 1300,
        [PartsCatalog.Es350] = 1250,
    };

    // Service-intensity multiplier so UC6 shows genuine model differences (Haval H9 > Patrol > … > ES 350).
    private static readonly Dictionary<string, double> ModelIntensity = new(StringComparer.Ordinal)
    {
        [PartsCatalog.HavalH9] = 1.5,
        [PartsCatalog.Patrol] = 1.25,
        [PartsCatalog.Corolla] = 1.0,
        [PartsCatalog.Camry] = 0.9,
        [PartsCatalog.Es350] = 0.8,
    };

    public async Task<SyntheticImportResult> ImportAsync(SyntheticGenerationSettings? settings = null, CancellationToken ct = default)
    {
        settings ??= SyntheticGenerationSettings.Default;

        var sales = await db.SalesFacts.AsNoTracking()
            .Select(s => new SalesRow(s.Model, s.Variant, s.Location, s.Colour, s.Year, s.Month, s.UnitsSold))
            .ToListAsync(ct);

        if (sales.Count == 0)
        {
            // Nothing to derive from — sample sales must be imported first.
            return new SyntheticImportResult("skipped-no-sales", 0, 0, 0, 0, string.Empty);
        }

        sales = sales
            .OrderBy(s => s.Year).ThenBy(s => s.Month)
            .ThenBy(s => s.Location, StringComparer.Ordinal)
            .ThenBy(s => s.Model, StringComparer.Ordinal)
            .ThenBy(s => s.Variant, StringComparer.Ordinal)
            .ThenBy(s => s.Colour, StringComparer.Ordinal)
            .ToList();

        var maxMonth = sales.Max(s => new DateOnly(s.Year, s.Month, 1));
        var checksum = Checksum(settings, sales.Count, maxMonth);

        var existing = await db.IngestionBatches.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SourceObject == SourceObject && x.Checksum == checksum && x.Status == "completed", ct);
        if (existing is not null)
        {
            return new SyntheticImportResult("skipped", 0, 0, 0, 0, checksum);
        }

        var batchId = DeterministicRandom.DeterministicGuid($"batch|{SourceObject}|{checksum}");
        var now = DateTimeOffset.UtcNow;
        var generated = Generate(sales, maxMonth, settings, batchId, now);

        await PurgeStaleSyntheticAsync(ct);
        await PersistAsync(batchId, checksum, generated, ct);
        return new SyntheticImportResult(
            "imported", generated.Vehicles.Count, generated.Events.Count, generated.Parts.Count, generated.Usages.Count, checksum);
    }

    private sealed record Generated(
        List<VehicleSale> Vehicles,
        List<ServiceEvent> Events,
        List<PartUsage> Usages,
        List<Part> Parts,
        List<PartCompatibility> Compatibilities,
        List<PartSupersession> Supersessions);

    private Generated Generate(
        IReadOnlyList<SalesRow> sales, DateOnly maxMonth, SyntheticGenerationSettings settings, Guid batchId, DateTimeOffset now)
    {
        var vehicles = new List<VehicleSale>();
        var events = new List<ServiceEvent>();
        var usages = new List<PartUsage>();

        var (parts, compat, supers, partIdByNumber) = BuildCatalog(batchId, now);

        var density = Math.Clamp(settings.Density, 0.0001, 1.0);

        foreach (var row in sales)
        {
            var units = ScaleUnits(row.UnitsSold, density);
            var saleMonth = new DateOnly(row.Year, row.Month, 1);
            var monthKey = $"{row.Year:D4}-{row.Month:D2}";
            var monthlyKmBase = ModelBaseKm.GetValueOrDefault(row.Model, 1450);
            var intensity = ModelIntensity.GetValueOrDefault(row.Model, 1.0);

            for (var index = 0; index < units; index++)
            {
                var vin = Vin(settings.Seed, row.Model, row.Variant, row.Location, monthKey, index);
                vehicles.Add(new VehicleSale
                {
                    Id = DeterministicRandom.DeterministicGuid($"vehicle|{vin}"),
                    Vin = vin,
                    Model = row.Model,
                    Variant = row.Variant,
                    Colour = row.Colour,
                    Location = row.Location,
                    SaleMonth = saleMonth,
                    SaleYear = row.Year,
                    IngestionBatchId = batchId,
                    IngestedAtUtc = now,
                });

                GenerateForVehicle(
                    vin, row, saleMonth, maxMonth, monthlyKmBase, intensity, settings, partIdByNumber, batchId, now, events, usages);
            }
        }

        return new Generated(vehicles, events, usages, parts, compat, supers);
    }

    private void GenerateForVehicle(
        string vin, SalesRow row, DateOnly saleMonth, DateOnly maxMonth, int monthlyKmBase, double intensity,
        SyntheticGenerationSettings settings, IReadOnlyDictionary<string, Guid> partIdByNumber,
        Guid batchId, DateTimeOffset now, List<ServiceEvent> events, List<PartUsage> usages)
    {
        var horizon = MonthsBetween(saleMonth, maxMonth);
        if (horizon <= 0)
        {
            return; // Recently sold (censored) — no service history yet.
        }

        var kmRng = DeterministicRandom.FromKey(settings.Seed, $"{vin}|km");
        var monthlyKm = Math.Max(400, monthlyKmBase + kmRng.NextInt(-250, 251));

        var routineIndex = 0;
        var eventSeq = 0;

        for (var m = 1; m <= horizon; m++)
        {
            var km = m * monthlyKm;
            var prevKm = (m - 1) * monthlyKm;
            var serviceDate = saleMonth.AddMonths(m);
            var band = MileageBand(km);

            // Routine: one per crossed 10,000 km boundary.
            if (km / 10_000 > prevKm / 10_000)
            {
                var laborRng = DeterministicRandom.FromKey(settings.Seed, $"{vin}|routine|{m}");
                AddEvent(vin, row, "Routine", serviceDate, m, km, band, 1.0 + (0.5 * laborRng.NextDouble()),
                    routineIndex, partIdByNumber, batchId, now, events, usages, ref eventSeq);
                routineIndex++;
            }

            // Warranty: low frequency, only inside the warranty window.
            if (m <= settings.WarrantyMonths && km <= settings.WarrantyKm)
            {
                var warRng = DeterministicRandom.FromKey(settings.Seed, $"{vin}|warranty|{m}");
                if (warRng.NextDouble() < settings.WarrantyMonthlyProbability * intensity)
                {
                    AddEvent(vin, row, "Warranty", serviceDate, m, km, band, 1.5,
                        routineIndex, partIdByNumber, batchId, now, events, usages, ref eventSeq, warRng);
                }
            }

            // Repair: intermittent, rising with age and mileage.
            var repRng = DeterministicRandom.FromKey(settings.Seed, $"{vin}|repair|{m}");
            var repairProb = settings.RepairBaseProbability * intensity * (1 + (km / 100_000.0));
            if (repRng.NextDouble() < repairProb)
            {
                var laborRng = DeterministicRandom.FromKey(settings.Seed, $"{vin}|repairlabor|{m}");
                AddEvent(vin, row, "Repair", serviceDate, m, km, band, 2.0 + (2.0 * laborRng.NextDouble()),
                    routineIndex, partIdByNumber, batchId, now, events, usages, ref eventSeq, repRng);
            }
        }

        // Recall: deterministic, batch-applied to matching model-year vehicles.
        var recall = PartsCatalog.Recalls.FirstOrDefault(r => r.Model == row.Model && r.SaleYear == row.Year);
        if (recall is not null && recall.MonthsAfterSale <= horizon)
        {
            var km = recall.MonthsAfterSale * monthlyKm;
            var serviceDate = saleMonth.AddMonths(recall.MonthsAfterSale);
            var rng = DeterministicRandom.FromKey(settings.Seed, $"{vin}|recall");
            AddEvent(vin, row, "Recall", serviceDate, recall.MonthsAfterSale, km, MileageBand(km), (double)recall.LaborHours,
                routineIndex, partIdByNumber, batchId, now, events, usages, ref eventSeq, rng);
        }
    }

    private static void AddEvent(
        string vin, SalesRow row, string serviceType, DateOnly serviceDate, int monthsSinceSale, int mileageKm, string band,
        double laborHours, int routineIndex, IReadOnlyDictionary<string, Guid> partIdByNumber,
        Guid batchId, DateTimeOffset now, List<ServiceEvent> events, List<PartUsage> usages, ref int eventSeq,
        DeterministicRandom? consumeRng = null)
    {
        var eventId = DeterministicRandom.DeterministicGuid($"service|{vin}|{eventSeq}");
        eventSeq++;

        events.Add(new ServiceEvent
        {
            Id = eventId,
            Vin = vin,
            Model = row.Model,
            Variant = row.Variant,
            Location = row.Location,
            ServiceDate = serviceDate,
            MonthsSinceSale = monthsSinceSale,
            MileageKm = mileageKm,
            MileageBand = band,
            ServiceType = serviceType,
            LaborHours = Math.Round((decimal)laborHours, 2),
            IngestionBatchId = batchId,
            IngestedAtUtc = now,
        });

        var rng = consumeRng ?? DeterministicRandom.FromKey(0, $"{vin}|consume|{eventSeq}");
        foreach (var (partNumber, qty) in PartsCatalog.Consume(row.Model, serviceType, mileageKm, routineIndex, serviceDate, rng))
        {
            if (!partIdByNumber.TryGetValue(partNumber, out var partId))
            {
                continue;
            }

            usages.Add(new PartUsage
            {
                Id = DeterministicRandom.DeterministicGuid($"usage|{eventId}|{partNumber}"),
                PartId = partId,
                Vin = vin,
                Model = row.Model,
                ServiceEventId = eventId,
                UsageDate = serviceDate,
                Quantity = qty,
                IngestionBatchId = batchId,
                IngestedAtUtc = now,
            });
        }
    }

    private static (List<Part>, List<PartCompatibility>, List<PartSupersession>, Dictionary<string, Guid>) BuildCatalog(
        Guid batchId, DateTimeOffset now)
    {
        var parts = new List<Part>();
        var compat = new List<PartCompatibility>();
        var idByNumber = new Dictionary<string, Guid>(StringComparer.Ordinal);

        foreach (var c in PartsCatalog.Parts)
        {
            var id = DeterministicRandom.DeterministicGuid($"part|{c.PartNumber}");
            idByNumber[c.PartNumber] = id;
            parts.Add(new Part
            {
                Id = id,
                PartNumber = c.PartNumber,
                Name = c.Name,
                Category = c.Category,
                UnitCost = c.UnitCost,
                LeadTimeDays = c.LeadTimeDays,
                CurrentStock = c.CurrentStock,
                InboundStock = c.InboundStock,
                IsActive = c.IsActive,
                IngestionBatchId = batchId,
                IngestedAtUtc = now,
            });

            var models = c.Models.Count == 0
                ? new[] { PartsCatalog.Patrol, PartsCatalog.Corolla, PartsCatalog.HavalH9, PartsCatalog.Camry, PartsCatalog.Es350 }
                : c.Models.ToArray();
            foreach (var model in models)
            {
                compat.Add(new PartCompatibility
                {
                    Id = DeterministicRandom.DeterministicGuid($"compat|{c.PartNumber}|{model}"),
                    PartId = id,
                    Model = model,
                    IngestionBatchId = batchId,
                    IngestedAtUtc = now,
                });
            }
        }

        // Second pass: wire the active-successor pointer now that every part has an id.
        foreach (var c in PartsCatalog.Parts)
        {
            if (c.SupersededByPartNumber is { } successor && idByNumber.TryGetValue(successor, out var successorId))
            {
                parts.Single(p => p.PartNumber == c.PartNumber).SupersededByPartId = successorId;
            }
        }

        var supers = PartsCatalog.Supersessions.Select(s => new PartSupersession
        {
            Id = DeterministicRandom.DeterministicGuid($"supersession|{s.OldPartNumber}|{s.NewPartNumber}"),
            OldPartId = idByNumber[s.OldPartNumber],
            NewPartId = idByNumber[s.NewPartNumber],
            EffectiveDate = s.EffectiveDate,
            IngestionBatchId = batchId,
            IngestedAtUtc = now,
        }).ToList();

        return (parts, compat, supers, idByNumber);
    }

    /// <summary>
    /// Removes any prior synthetic dataset (a stale generation version) so exactly one synthetic dataset
    /// ever exists. These six tables only ever hold synthetic-demo data, so this is safe and total.
    /// </summary>
    private async Task PurgeStaleSyntheticAsync(CancellationToken ct)
    {
        await db.PartUsages.ExecuteDeleteAsync(ct);
        await db.ServiceEvents.ExecuteDeleteAsync(ct);
        await db.VehicleSales.ExecuteDeleteAsync(ct);
        await db.PartSupersessions.ExecuteDeleteAsync(ct);
        await db.PartCompatibilities.ExecuteDeleteAsync(ct);
        await db.Parts.ExecuteDeleteAsync(ct);
        await db.IngestionBatches.Where(b => b.SourceObject == SourceObject).ExecuteDeleteAsync(ct);
    }

    private async Task PersistAsync(Guid batchId, string checksum, Generated g, CancellationToken ct)
    {
        var autoDetect = db.ChangeTracker.AutoDetectChangesEnabled;
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await InsertChunkedAsync(g.Parts, ct);
            await InsertChunkedAsync(g.Compatibilities, ct);
            await InsertChunkedAsync(g.Supersessions, ct);
            await InsertChunkedAsync(g.Vehicles, ct);
            await InsertChunkedAsync(g.Events, ct);
            await InsertChunkedAsync(g.Usages, ct);

            db.IngestionBatches.Add(new IngestionBatch
            {
                Id = batchId,
                SourceSystem = SourceSystem,
                SourceObject = SourceObject,
                Checksum = checksum,
                FileName = "synthetic-after-sales-and-parts",
                RecordCount = g.Vehicles.Count + g.Events.Count + g.Usages.Count + g.Parts.Count,
                Status = "completed",
                StartedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();

            await tx.CommitAsync(ct);
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = autoDetect;
        }
    }

    private async Task InsertChunkedAsync<T>(IReadOnlyList<T> entities, CancellationToken ct)
        where T : class
    {
        for (var i = 0; i < entities.Count; i += InsertChunk)
        {
            var count = Math.Min(InsertChunk, entities.Count - i);
            for (var j = 0; j < count; j++)
            {
                db.Add(entities[i + j]);
            }

            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }
    }

    private static int ScaleUnits(int units, double density)
    {
        if (units <= 0)
        {
            return 0;
        }

        return density >= 1.0 ? units : Math.Max(1, (int)Math.Round(units * density, MidpointRounding.AwayFromZero));
    }

    private static string Vin(ulong seed, string model, string variant, string location, string monthKey, int index)
    {
        var cell = DeterministicRandom.Hash(seed, $"{model}|{variant}|{location}|{monthKey}");
        return "SYN" + DeterministicRandom.ToBase36(cell, 11) + DeterministicRandom.ToBase36((ulong)index, 3);
    }

    private static int MonthsBetween(DateOnly from, DateOnly to)
        => ((to.Year - from.Year) * 12) + (to.Month - from.Month);

    private static string MileageBand(int km) => km switch
    {
        < 20_000 => "0–20k",
        < 60_000 => "20–60k",
        < 120_000 => "60–120k",
        _ => "120k+",
    };

    private static string Checksum(SyntheticGenerationSettings settings, int salesRowCount, DateOnly maxMonth)
    {
        var key = string.Create(CultureInfo.InvariantCulture,
            $"{CatalogVersion}|{settings.Seed}|{settings.Density:R}|{settings.WarrantyMonths}|{settings.WarrantyKm}|{settings.WarrantyMonthlyProbability:R}|{settings.RepairBaseProbability:R}|{salesRowCount}|{maxMonth:yyyy-MM}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
    }
}
