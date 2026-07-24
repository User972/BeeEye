namespace BeeEye.Persistence.Entities;

/// <summary>
/// Which vehicle models a <see cref="Part"/> fits (UC7). A part may fit several models and a model
/// pulls many parts — this many-to-many drives demand attribution and installed-base estimation.
/// </summary>
public class PartCompatibility
{
    public Guid Id { get; set; }

    public Guid PartId { get; set; }

    public string Model { get; set; } = string.Empty;

    public Guid IngestionBatchId { get; set; }
    public DateTimeOffset IngestedAtUtc { get; set; }
}
