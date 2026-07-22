namespace BeeEye.Persistence.Entities;

/// <summary>
/// One part consumed by a <see cref="ServiceEvent"/> (UC7) — the intermittent-demand fact table. The
/// monthly usage series built from these rows is dense (explicit zeros for months with no consumption).
/// Synthetic-demo data — no PII.
/// </summary>
public class PartUsage
{
    public Guid Id { get; set; }

    public Guid PartId { get; set; }

    /// <summary>Synthetic vehicle reference the part was fitted to.</summary>
    public string Vin { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public Guid ServiceEventId { get; set; }

    public DateOnly UsageDate { get; set; }

    public int Quantity { get; set; }

    public Guid IngestionBatchId { get; set; }
    public DateTimeOffset IngestedAtUtc { get; set; }
}
