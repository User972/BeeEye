using BeeEye.Shared.Decisions;

namespace BeeEye.Persistence.Entities;

/// <summary>
/// A rule-engine recommendation, captured verbatim as an immutable business record
/// (<c>docs/adr/0006-recommendation-decision-workflow.md</c> §2).
/// <para>
/// <b>Written once and never edited.</b> The human's decision, any modification and the realised
/// outcome are stored as separate linked records; this row always shows exactly what the engine said
/// and on what basis. A newer run <i>supersedes</i> rather than overwrites.
/// </para>
/// </summary>
public class Recommendation
{
    public Guid Id { get; set; }

    /// <summary>
    /// Deterministic natural key: the same subject, ruleset and analysis date always produce the same
    /// value. Unique, so re-running a generation is idempotent rather than duplicating records
    /// (ADR 0007).
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>What the recommendation is about, e.g. a stock id or a model·variant key.</summary>
    public string SubjectRef { get; set; } = string.Empty;

    /// <summary>Business area that raised it, e.g. "Inventory".</summary>
    public string Area { get; set; } = string.Empty;

    /// <summary>Stable rule identifier, e.g. "D-INV-1".</summary>
    public string RuleId { get; set; } = string.Empty;

    // ---- The frozen engine output ----------------------------------------

    public string Action { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;

    /// <summary>Supporting evidence, serialised as JSON so the exact text shown to the approver survives.</summary>
    public string EvidenceJson { get; set; } = "[]";

    public string ExpectedOutcome { get; set; } = string.Empty;

    /// <summary>Confidence band as the engine expressed it: High / Medium / Low.</summary>
    public string Confidence { get; set; } = string.Empty;

    /// <summary>Assumptions, serialised as JSON.</summary>
    public string AssumptionsJson { get; set; } = "[]";

    /// <summary>Financial exposure or upside, in SAR. Money is decimal, never floating point.</summary>
    public decimal ImpactSar { get; set; }

    /// <summary>0–100 priority as scored at generation time.</summary>
    public int Priority { get; set; }

    /// <summary>Role expected to own the decision.</summary>
    public string OwnerRole { get; set; } = string.Empty;

    /// <summary>True when the recommendation derives from synthetic demo data and must be labelled.</summary>
    public bool IsDemoData { get; set; }

    // ---- Provenance stamps -----------------------------------------------

    public string RulesetVersion { get; set; } = string.Empty;
    public string DatasetVersion { get; set; } = string.Empty;

    /// <summary>The pinned analysis date the recommendation was computed against — never wall-clock "now".</summary>
    public DateOnly AnalysisDate { get; set; }

    // ---- Lifecycle -------------------------------------------------------

    /// <summary>
    /// Cached projection of <see cref="RecommendationStatusEvent"/> for query efficiency. The event log
    /// is the source of truth; this column is only ever updated by the transition service.
    /// </summary>
    public RecommendationStatus CurrentStatus { get; set; } = RecommendationLifecycle.InitialStatus;

    /// <summary>Drives the Expired transition. Null means no validity window was set.</summary>
    public DateTimeOffset? ValidUntilUtc { get; set; }

    /// <summary>The newer recommendation that replaced this one, when superseded.</summary>
    public Guid? SupersededByRecommendationId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Optimistic-concurrency token; a stale writer loses rather than silently overwriting.</summary>
    public uint Version { get; set; }

    public ICollection<RecommendationStatusEvent> StatusEvents { get; set; } = [];
}

/// <summary>
/// One appended lifecycle transition. The append-only log is the source of truth for a
/// recommendation's state; <see cref="Recommendation.CurrentStatus"/> is a projection of it.
/// Rows are never updated or deleted.
/// </summary>
public class RecommendationStatusEvent
{
    public Guid Id { get; set; }

    public Guid RecommendationId { get; set; }

    /// <summary>Null for the first event, which records the initial <c>Generated</c> state.</summary>
    public RecommendationStatus? FromStatus { get; set; }

    public RecommendationStatus ToStatus { get; set; }

    /// <summary>
    /// Who caused the transition: the stable subject id of a human, or "system" for engine- and
    /// job-driven transitions. Never a display name — ADR 0006 needs this to be stable.
    /// </summary>
    public string Actor { get; set; } = string.Empty;

    /// <summary>Why. Mandatory for a rejection.</summary>
    public string? Reason { get; set; }

    public DateTimeOffset AtUtc { get; set; }

    public Recommendation? Recommendation { get; set; }
}
