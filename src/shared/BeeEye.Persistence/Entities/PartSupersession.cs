namespace BeeEye.Persistence.Entities;

/// <summary>
/// A supersession link: <see cref="OldPartId"/> is replaced by <see cref="NewPartId"/> from
/// <see cref="EffectiveDate"/> (UC7). Chains are retained for audit and to read legacy transactions;
/// forecasting rolls the old part's history onto the successor.
/// </summary>
public class PartSupersession
{
    public Guid Id { get; set; }

    public Guid OldPartId { get; set; }
    public Guid NewPartId { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public Guid IngestionBatchId { get; set; }
    public DateTimeOffset IngestedAtUtc { get; set; }
}
