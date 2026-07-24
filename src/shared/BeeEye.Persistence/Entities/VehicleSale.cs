namespace BeeEye.Persistence.Entities;

/// <summary>
/// One synthetic vehicle expanded from a monthly <see cref="SalesFact"/> row (UC6). The VIN is a
/// deterministic <b>synthetic</b> surrogate — no real VIN, no customer PII. Provenance is carried on
/// the ingestion batch (SourceSystem = "synthetic-demo").
/// </summary>
public class VehicleSale
{
    public Guid Id { get; set; }

    /// <summary>Synthetic, unique vehicle reference (not a real VIN; prefixed "SYN").</summary>
    public string Vin { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;
    public string Colour { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;

    /// <summary>First day of the sale month.</summary>
    public DateOnly SaleMonth { get; set; }

    public int SaleYear { get; set; }

    public Guid IngestionBatchId { get; set; }
    public DateTimeOffset IngestedAtUtc { get; set; }
}
