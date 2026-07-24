namespace BeeEye.Persistence.Entities;

/// <summary>
/// Records an ingestion run. Batch identity is (SourceObject, Checksum): reprocessing
/// an extract with the same checksum is skipped, keeping ingestion idempotent.
/// </summary>
public class IngestionBatch
{
    public Guid Id { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string SourceObject { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public string Status { get; set; } = "completed";
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
}
