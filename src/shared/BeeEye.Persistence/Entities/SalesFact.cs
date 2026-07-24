namespace BeeEye.Persistence.Entities;

/// <summary>
/// A monthly sales observation at location · model · variant grain (plus attributes),
/// mirroring the source sales-history extract. Denormalised star-schema fact.
/// </summary>
public class SalesFact
{
    public Guid Id { get; set; }

    /// <summary>First day of the reporting month.</summary>
    public DateOnly SaleMonth { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }

    public string Location { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;
    public string Colour { get; set; } = string.Empty;
    public string Interior { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;

    public int UnitsSold { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Revenue { get; set; }
    public string Currency { get; set; } = "SAR";

    public bool DiscountApplied { get; set; }
    public int DiscountPct { get; set; }
    public bool IsRamadan { get; set; }
    public DateOnly DateOfManufacture { get; set; }

    // ---- Lineage / idempotency ----

    /// <summary>Deterministic content hash of the business columns plus the row's position
    /// in its extract — unique, so reprocessing the same extract never duplicates facts
    /// while identical rows within one extract stay distinguishable.</summary>
    public string RowHash { get; set; } = string.Empty;

    public Guid IngestionBatchId { get; set; }
    public DateTimeOffset IngestedAtUtc { get; set; }
}
