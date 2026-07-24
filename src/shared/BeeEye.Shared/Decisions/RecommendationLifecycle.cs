namespace BeeEye.Shared.Decisions;

/// <summary>
/// Lifecycle states of a recommendation, from <c>docs/adr/0006-recommendation-decision-workflow.md</c> §3.
/// <para>
/// The current state is a <b>projection over an append-only status log</b>, never a mutable field that
/// erases history. Nothing in the platform sets a status directly; every change is an appended,
/// audited transition.
/// </para>
/// </summary>
public enum RecommendationStatus
{
    /// <summary>Written by an engine run and frozen. The only state a recommendation can start in.</summary>
    Generated,

    /// <summary>A human has claimed the item and opened a decision on it.</summary>
    UnderReview,

    /// <summary>Approved as recommended.</summary>
    Accepted,

    /// <summary>Approved with a change; the modification is stored as a delta beside the frozen original.</summary>
    AcceptedModified,

    /// <summary>Declined. Terminal. A reason is mandatory.</summary>
    Rejected,

    /// <summary>The validity window lapsed before review. Terminal.</summary>
    Expired,

    /// <summary>A later run produced a fresher recommendation for the same subject. Terminal.</summary>
    Superseded,

    /// <summary>A human confirmed the action was executed downstream. BeeEye never writes to Oracle Fusion.</summary>
    Implemented,

    /// <summary>The realised effect was measured, closing the learning loop. Terminal.</summary>
    OutcomeRecorded,
}

/// <summary>Why a transition was refused. Carried to the caller so the reason is explainable, not a bare false.</summary>
public enum TransitionRefusal
{
    None,

    /// <summary>The pair of states is not an edge in the state machine.</summary>
    NotAllowed,

    /// <summary>The source state is terminal; nothing follows it.</summary>
    SourceIsTerminal,

    /// <summary>Expiry is suspended once a human owns the item (ADR 0006 §3 guard).</summary>
    ExpiryBlockedByReview,

    /// <summary>Supersession is blocked while an approval is in flight (ADR 0006 §3 guard).</summary>
    SupersessionBlockedByApprovalInFlight,

    /// <summary>Rejection requires a reason.</summary>
    ReasonRequired,
}

/// <summary>The outcome of evaluating a proposed transition.</summary>
/// <param name="Allowed">True when the transition may be appended to the log.</param>
/// <param name="Refusal">Why it was refused; <see cref="TransitionRefusal.None"/> when allowed.</param>
public readonly record struct TransitionResult(bool Allowed, TransitionRefusal Refusal)
{
    public static TransitionResult Ok() => new(true, TransitionRefusal.None);

    public static TransitionResult Refused(TransitionRefusal refusal) => new(false, refusal);

    /// <summary>A safe, non-technical explanation suitable for returning to a caller.</summary>
    public string Explain() => Refusal switch
    {
        TransitionRefusal.None => "Allowed.",
        TransitionRefusal.NotAllowed => "That is not a valid next step for this recommendation.",
        TransitionRefusal.SourceIsTerminal => "This recommendation has already reached a final state.",
        TransitionRefusal.ExpiryBlockedByReview =>
            "This recommendation is under review, so it will not expire while someone owns it.",
        TransitionRefusal.SupersessionBlockedByApprovalInFlight =>
            "An approval is in progress, so this recommendation cannot be superseded yet.",
        TransitionRefusal.ReasonRequired => "A reason is required to reject a recommendation.",
        _ => "That change is not permitted.",
    };
}

/// <summary>
/// The recommendation lifecycle state machine (ADR 0006 §3), including its guards.
/// <para>
/// Pure and deterministic — no clock, no I/O — so every edge and every guard is exhaustively
/// unit-testable. All writers must go through this type; there is no path that sets a status without
/// validating the transition first.
/// </para>
/// </summary>
public static class RecommendationLifecycle
{
    /// <summary>States from which nothing follows.</summary>
    public static readonly IReadOnlySet<RecommendationStatus> Terminal =
        new HashSet<RecommendationStatus>
        {
            RecommendationStatus.Rejected,
            RecommendationStatus.Expired,
            RecommendationStatus.Superseded,
            RecommendationStatus.OutcomeRecorded,
        };

    /// <summary>The edges of the state machine.</summary>
    private static readonly IReadOnlyDictionary<RecommendationStatus, IReadOnlySet<RecommendationStatus>> Edges =
        new Dictionary<RecommendationStatus, IReadOnlySet<RecommendationStatus>>
        {
            [RecommendationStatus.Generated] = new HashSet<RecommendationStatus>
            {
                RecommendationStatus.UnderReview,
                RecommendationStatus.Superseded,
                RecommendationStatus.Expired,
            },
            [RecommendationStatus.UnderReview] = new HashSet<RecommendationStatus>
            {
                RecommendationStatus.Accepted,
                RecommendationStatus.AcceptedModified,
                RecommendationStatus.Rejected,
                RecommendationStatus.Superseded,

                // Present as an edge but always refused by the expiry guard below. The expiry job
                // legitimately attempts this transition on every unreviewed record, so it earns a
                // specific, explainable refusal ("someone owns it") rather than the generic
                // "not a valid next step" an absent edge would produce.
                RecommendationStatus.Expired,
            },
            [RecommendationStatus.Accepted] = new HashSet<RecommendationStatus>
            {
                RecommendationStatus.Implemented,
            },
            [RecommendationStatus.AcceptedModified] = new HashSet<RecommendationStatus>
            {
                RecommendationStatus.Implemented,
            },
            [RecommendationStatus.Implemented] = new HashSet<RecommendationStatus>
            {
                RecommendationStatus.OutcomeRecorded,
            },
            [RecommendationStatus.Rejected] = new HashSet<RecommendationStatus>(),
            [RecommendationStatus.Expired] = new HashSet<RecommendationStatus>(),
            [RecommendationStatus.Superseded] = new HashSet<RecommendationStatus>(),
            [RecommendationStatus.OutcomeRecorded] = new HashSet<RecommendationStatus>(),
        };

    /// <summary>The state every recommendation starts in.</summary>
    public const RecommendationStatus InitialStatus = RecommendationStatus.Generated;

    /// <summary>True when nothing can follow <paramref name="status"/>.</summary>
    public static bool IsTerminal(RecommendationStatus status) => Terminal.Contains(status);

    /// <summary>States reachable in one step from <paramref name="from"/>, ignoring guards.</summary>
    public static IReadOnlySet<RecommendationStatus> NextStates(RecommendationStatus from) =>
        Edges.TryGetValue(from, out var next) ? next : new HashSet<RecommendationStatus>();

    /// <summary>
    /// Evaluates a proposed transition, including the ADR 0006 §3 guards.
    /// </summary>
    /// <param name="from">Current projected status.</param>
    /// <param name="to">Proposed next status.</param>
    /// <param name="approvalInFlight">
    /// True when an approval step is open. Blocks supersession so a race cannot erase a decision a
    /// human is actively making.
    /// </param>
    /// <param name="hasReason">True when a reason accompanies the transition. Required to reject.</param>
    public static TransitionResult CanTransition(
        RecommendationStatus from,
        RecommendationStatus to,
        bool approvalInFlight = false,
        bool hasReason = true)
    {
        if (IsTerminal(from))
        {
            return TransitionResult.Refused(TransitionRefusal.SourceIsTerminal);
        }

        if (!NextStates(from).Contains(to))
        {
            return TransitionResult.Refused(TransitionRefusal.NotAllowed);
        }

        // Guard: expiry is suspended once a human owns the item, so it cannot silently lapse mid-decision.
        if (to == RecommendationStatus.Expired && from == RecommendationStatus.UnderReview)
        {
            return TransitionResult.Refused(TransitionRefusal.ExpiryBlockedByReview);
        }

        // Guard: an in-flight approval completes before a newer run may supersede the record.
        if (to == RecommendationStatus.Superseded && approvalInFlight)
        {
            return TransitionResult.Refused(TransitionRefusal.SupersessionBlockedByApprovalInFlight);
        }

        // A rejection without a reason destroys the learning loop the ADR exists to protect.
        if (to == RecommendationStatus.Rejected && !hasReason)
        {
            return TransitionResult.Refused(TransitionRefusal.ReasonRequired);
        }

        return TransitionResult.Ok();
    }
}
