namespace BeeEye.Persistence.Entities;

/// <summary>
/// A spare-part master record (UC7). Supersession is modelled both by the nullable
/// <see cref="SupersededByPartId"/> pointer (the active successor) and the auditable
/// <see cref="PartSupersession"/> chain. Synthetic-demo catalogue — no PII.
/// </summary>
public class Part
{
    public Guid Id { get; set; }

    /// <summary>Unique catalogue part number.</summary>
    public string PartNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    /// <summary>Unit cost (SAR) — money as decimal, higher precision than sale money.</summary>
    public decimal UnitCost { get; set; }

    public int LeadTimeDays { get; set; }
    public int CurrentStock { get; set; }
    public int InboundStock { get; set; }

    /// <summary>The active successor part when this part has been superseded; otherwise null.</summary>
    public Guid? SupersededByPartId { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid IngestionBatchId { get; set; }
    public DateTimeOffset IngestedAtUtc { get; set; }
}
