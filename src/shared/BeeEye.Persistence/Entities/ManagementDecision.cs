namespace BeeEye.Persistence.Entities;

/// <summary>
/// What a human decided about a recommendation. Stored as text, never as an ordinal, so a renumbered
/// enum cannot silently reinterpret history.
/// </summary>
public enum DecisionOutcome
{
    /// <summary>Claimed and being worked. Exactly one open decision may exist per recommendation.</summary>
    Open,

    /// <summary>Approved as recommended.</summary>
    Accepted,

    /// <summary>Approved with a change; the delta is stored beside the untouched original.</summary>
    AcceptedModified,

    /// <summary>Declined. A note is mandatory.</summary>
    Rejected,
}

/// <summary>State of one step in an approval chain.</summary>
public enum ApprovalStepStatus
{
    Pending,
    Approved,
    Declined,
}

/// <summary>
/// A human's response to a frozen <see cref="Recommendation"/>
/// (<c>docs/adr/0006-recommendation-decision-workflow.md</c> §4, canonical model Cluster 8).
/// <para>
/// <b>This record is the human layer, appended around the original — never an edit of it.</b> The
/// recommendation says what the engine advised and on what basis; this row says who claimed it, what
/// they decided, what they changed, and when. Reading the two side by side is the central promise of
/// ADR 0006, and it only holds because nothing here writes back into the recommendation.
/// </para>
/// <para>
/// There is <b>no delete path</b>. A decision that should not have been taken is rejected — a terminal
/// state that ends the record's life while preserving why.
/// </para>
/// </summary>
public class ManagementDecision
{
    /// <summary>Time-ordered v7 GUID, so insertion order is readable from the key itself.</summary>
    public Guid Id { get; set; }

    /// <summary>The exact recommendation this decision acted on. Restrict, never cascade.</summary>
    public Guid RecommendationId { get; set; }

    /// <summary>
    /// The client-supplied <c>Idempotency-Key</c> that created this decision (ADR 0007 §2.1). Unique,
    /// so a retried claim converges on the record it already created instead of opening a second one.
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>
    /// Stable subject id of whoever claimed the recommendation — never a display name. ADR 0006 needs
    /// this to survive a rename, a re-grant and a directory migration.
    /// </summary>
    public string OpenedBy { get; set; } = string.Empty;

    public DateTimeOffset OpenedAtUtc { get; set; }

    /// <summary>Where the decision stands. <see cref="DecisionOutcome.Open"/> until someone decides.</summary>
    public DecisionOutcome Outcome { get; set; } = DecisionOutcome.Open;

    /// <summary>Stable subject id of the decider. Null while the decision is open.</summary>
    public string? DecidedBy { get; set; }

    public DateTimeOffset? DecidedAtUtc { get; set; }

    /// <summary>Why. <b>Mandatory for a rejection</b> — a rejection without a reason destroys the learning loop.</summary>
    public string? Note { get; set; }

    /// <summary>
    /// The modification delta as JSON, e.g. <c>{"field":"discount_pct","from":15,"to":10}</c>. Null
    /// unless the outcome is <see cref="DecisionOutcome.AcceptedModified"/>. A delta, never an edit:
    /// the recommended value stays readable beside the human's.
    /// </summary>
    public string? ModificationJson { get; set; }

    /// <summary>
    /// When a human confirmed the action was executed in the enterprise systems. BeeEye never writes
    /// to Oracle Fusion (ADR 0006 §7) — this records a human's confirmation, not an integration.
    /// </summary>
    public DateTimeOffset? ImplementedAtUtc { get; set; }

    public string? ImplementedBy { get; set; }

    /// <summary>Optimistic-concurrency token (PostgreSQL xmin); a stale writer loses rather than overwriting.</summary>
    public uint Version { get; set; }

    public Recommendation? Recommendation { get; set; }

    public ICollection<ApprovalStep> ApprovalSteps { get; set; } = [];

    /// <summary>
    /// The realised result, once measured. Named <c>ActionOutcome</c> rather than <c>Outcome</c>
    /// because <see cref="Outcome"/> already means the human's verdict; the two are different things
    /// and sharing a name would invite exactly the confusion ADR 0006 is trying to remove.
    /// </summary>
    public ActionOutcome? ActionOutcome { get; set; }
}

/// <summary>
/// One step of a multi-step sign-off chain (canonical model Cluster 8).
/// <para>
/// <b>Append-only in spirit.</b> A step moves <c>Pending → Approved|Declined</c> exactly once; a step
/// already acted on is immutable, and re-acting is a conflict rather than an overwrite. The identity
/// of who signed matters more than the fact that something was signed.
/// </para>
/// </summary>
public class ApprovalStep
{
    public Guid Id { get; set; }

    public Guid DecisionId { get; set; }

    /// <summary>1-based position in the chain. Unique within a decision.</summary>
    public int StepNumber { get; set; }

    /// <summary>The role expected to act, taken from the recommendation's owner role.</summary>
    public string ApproverRole { get; set; } = string.Empty;

    public ApprovalStepStatus Status { get; set; } = ApprovalStepStatus.Pending;

    /// <summary>Stable subject id of whoever acted. Null while pending.</summary>
    public string? ActedBy { get; set; }

    public DateTimeOffset? ActedAtUtc { get; set; }

    public string? Note { get; set; }

    public ManagementDecision? Decision { get; set; }
}

/// <summary>
/// The realised effect of a decision, measured after the fact — the record that closes ADR 0006's
/// learning loop. At most one per decision (the canonical model's <c>||--o|</c>).
/// </summary>
public class ActionOutcome
{
    public Guid Id { get; set; }

    public Guid DecisionId { get; set; }

    /// <summary>What was measured, e.g. "Holding cost avoided".</summary>
    public string Metric { get; set; } = string.Empty;

    /// <summary>
    /// The measured value. Decimal with explicit precision — this is usually a SAR amount, and money
    /// is never floating point.
    /// </summary>
    public decimal RealisedValue { get; set; }

    /// <summary>Unit of <see cref="RealisedValue"/>, e.g. "SAR", "units", "days".</summary>
    public string? Unit { get; set; }

    public DateTimeOffset MeasuredAtUtc { get; set; }

    /// <summary>Stable subject id of whoever recorded the measurement.</summary>
    public string RecordedBy { get; set; } = string.Empty;

    public string? Note { get; set; }

    public ManagementDecision? Decision { get; set; }
}
