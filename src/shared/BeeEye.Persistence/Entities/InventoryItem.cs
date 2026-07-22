namespace BeeEye.Persistence.Entities;

/// <summary>A physical inventory unit, keyed by its source stock id (UC5).</summary>
public class InventoryItem
{
    public Guid Id { get; set; }

    /// <summary>Source natural key — unique, enabling idempotent upsert.</summary>
    public string StockId { get; set; } = string.Empty;

    public string ChassisNo { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;
    public string Colour { get; set; } = string.Empty;
    public string Interior { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;

    public DateOnly DateOfPurchase { get; set; }
    public DateOnly DateOfManufacture { get; set; }

    /// <summary>Meaning unconfirmed in the source — displayed but excluded from risk scoring.</summary>
    public DateOnly? ServiceDate { get; set; }

    public int LeadTimeDays { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal HoldingCostPerDay { get; set; }
    public string Currency { get; set; } = "SAR";

    public Guid IngestionBatchId { get; set; }
    public DateTimeOffset IngestedAtUtc { get; set; }
}
