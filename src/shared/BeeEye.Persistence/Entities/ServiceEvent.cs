namespace BeeEye.Persistence.Entities;

/// <summary>
/// A synthetic after-sales service visit for a <see cref="VehicleSale"/> (UC6). ServiceType is stored
/// as a string ("Routine" / "Repair" / "Warranty" / "Recall") so the persistence kernel stays free of
/// the analytics enum. Synthetic-demo data — no PII.
/// </summary>
public class ServiceEvent
{
    public Guid Id { get; set; }

    public string Vin { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;

    public DateOnly ServiceDate { get; set; }

    /// <summary>Whole months between the vehicle's sale and this visit — the time-since-sale driver.</summary>
    public int MonthsSinceSale { get; set; }

    public int MileageKm { get; set; }

    /// <summary>Bucketed odometer band at the visit (e.g. "0–20k").</summary>
    public string MileageBand { get; set; } = string.Empty;

    /// <summary>Routine | Repair | Warranty | Recall.</summary>
    public string ServiceType { get; set; } = string.Empty;

    public decimal LaborHours { get; set; }

    public Guid IngestionBatchId { get; set; }
    public DateTimeOffset IngestedAtUtc { get; set; }
}
