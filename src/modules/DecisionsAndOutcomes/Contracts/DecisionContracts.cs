using BeeEye.Shared.Web.Idempotency;

namespace BeeEye.Modules.DecisionsAndOutcomes.Contracts;

// ---------------------------------------------------------------------------------------------
// Request bodies. Each carries IIdempotentPayload so the Idempotency-Key fingerprint covers it —
// see IIdempotentPayload for why the marker is explicit rather than inferred.
// ---------------------------------------------------------------------------------------------

/// <summary>Accept a recommendation with a change (ADR 0006 §2.3). Stored as a delta, never an edit.</summary>
/// <param name="Field">The value being changed; one of the allowlisted modifiable fields.</param>
/// <param name="From">The value the engine recommended.</param>
/// <param name="To">The value the human chose.</param>
/// <param name="Rationale">Optional explanation of the change.</param>
public sealed record ModificationRequest(string Field, decimal From, decimal To, string? Rationale)
    : IIdempotentPayload;

/// <summary>Decline a recommendation. The note is mandatory — a rejection without a reason teaches nothing.</summary>
public sealed record RejectRequest(string Note) : IIdempotentPayload;

/// <summary>Sign off one step of the approval chain.</summary>
/// <param name="Approved">True to approve, false to decline.</param>
/// <param name="Note">Optional remark, retained on the step.</param>
public sealed record ApprovalRequest(bool Approved, string? Note) : IIdempotentPayload;

/// <summary>Record the realised effect of an implemented decision, closing the learning loop.</summary>
/// <param name="Metric">What was measured, e.g. "Holding cost avoided".</param>
/// <param name="RealisedValue">The measured value. Decimal — money is never floating point.</param>
/// <param name="Unit">Unit of the value, e.g. "SAR", "units", "days".</param>
/// <param name="Note">Optional commentary on how it was measured.</param>
public sealed record OutcomeRequest(string Metric, decimal RealisedValue, string? Unit, string? Note)
    : IIdempotentPayload;

// ---------------------------------------------------------------------------------------------
// Responses
// ---------------------------------------------------------------------------------------------

/// <summary>The modification delta as returned to a caller.</summary>
public sealed record ModificationDto(string Field, decimal From, decimal To, string? Rationale);

/// <summary>One step of the approval chain.</summary>
public sealed record ApprovalStepDto(
    int StepNumber,
    string ApproverRole,
    string Status,
    string? ActedBy,
    DateTimeOffset? ActedAtUtc,
    string? Note);

/// <summary>The realised outcome of a decision.</summary>
public sealed record ActionOutcomeDto(
    string Metric,
    decimal RealisedValue,
    string? Unit,
    DateTimeOffset MeasuredAtUtc,
    string RecordedBy,
    string? Note);

/// <summary>One appended lifecycle transition, as shown in the detail timeline.</summary>
public sealed record StatusEventDto(
    string? FromStatus,
    string ToStatus,
    string Actor,
    string? Reason,
    DateTimeOffset AtUtc);

/// <summary>The human layer: who claimed it, what they decided, and when.</summary>
public sealed record DecisionSummaryDto(
    Guid Id,
    string Outcome,
    string OpenedBy,
    DateTimeOffset OpenedAtUtc,
    string? DecidedBy,
    DateTimeOffset? DecidedAtUtc,
    string? Note,
    ModificationDto? Modification,
    string? ImplementedBy,
    DateTimeOffset? ImplementedAtUtc);

/// <summary>
/// One row of the Decision Log.
/// <para>
/// The row's identity is the <b>recommendation</b>, not the decision: the log is the governed trail of
/// every recommendation, and a record that nobody has claimed yet is exactly the row a reviewer needs
/// to see. <see cref="DecisionId"/> is null until someone claims it.
/// </para>
/// </summary>
/// <param name="AvailableActions">
/// The transitions this caller may actually perform right now — the state machine's legal next steps
/// intersected with the caller's permissions. The screen renders these and nothing else, so a control
/// is never offered that the server would refuse.
/// </param>
public sealed record DecisionLogItemDto(
    Guid RecommendationId,
    Guid? DecisionId,
    string RuleId,
    string SubjectRef,
    string Area,
    string Action,
    string Evidence,
    string Status,
    string? Outcome,
    decimal ImpactSar,
    int Priority,
    string OwnerRole,
    bool IsDemoData,
    string Source,
    DateTimeOffset CreatedAtUtc,
    string? DecidedBy,
    DateTimeOffset? DecidedAtUtc,
    ModificationDto? Modification,
    IReadOnlyList<string> AvailableActions);

/// <summary>A page of log rows, with the counts behind the status chip row.</summary>
/// <param name="Items">The rows on this page.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Rows per page, clamped server-side.</param>
/// <param name="TotalCount">Rows matching every filter, including the status filter.</param>
/// <param name="StatusCounts">
/// Count per lifecycle status, honouring every filter <b>except</b> status — otherwise selecting a
/// chip would zero every other chip and the row would stop being navigable.
/// </param>
public sealed record DecisionLogPageDto(
    IReadOnlyList<DecisionLogItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<string, int> StatusCounts);

/// <summary>The frozen engine output, exactly as it was written.</summary>
public sealed record RecommendationSnapshotDto(
    Guid Id,
    string RuleId,
    string SubjectRef,
    string Area,
    string Action,
    string Rationale,
    IReadOnlyList<string> Evidence,
    string ExpectedOutcome,
    string Confidence,
    IReadOnlyList<string> Assumptions,
    decimal ImpactSar,
    int Priority,
    string OwnerRole,
    bool IsDemoData,
    string RulesetVersion,
    string DatasetVersion,
    DateOnly AnalysisDate,
    string CurrentStatus,
    DateTimeOffset? ValidUntilUtc,
    Guid? SupersededByRecommendationId,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// The detail view: what the system recommended, beside what the human decided.
/// <para>
/// ADR 0006's central promise is that these two are readable side by side, months later and under
/// audit. That is why the frozen original travels with the decision rather than being fetched
/// separately and possibly from a different generation.
/// </para>
/// </summary>
public sealed record DecisionDetailDto(
    RecommendationSnapshotDto Recommendation,
    DecisionSummaryDto? Decision,
    IReadOnlyList<ApprovalStepDto> ApprovalSteps,
    IReadOnlyList<StatusEventDto> StatusEvents,
    ActionOutcomeDto? Outcome,
    IReadOnlyList<string> AvailableActions);

/// <summary>The result of a transition, returned by every write endpoint.</summary>
/// <param name="RecommendationId">The recommendation that moved.</param>
/// <param name="DecisionId">The decision record involved.</param>
/// <param name="Status">The lifecycle status the recommendation is now in.</param>
/// <param name="Outcome">The decision's outcome.</param>
/// <param name="Message">
/// A plain-language statement of what just happened, shown to the user verbatim. For
/// <c>Implemented</c> it states explicitly that BeeEye wrote nothing to Oracle Fusion.
/// </param>
public sealed record TransitionResponseDto(
    Guid RecommendationId,
    Guid DecisionId,
    string Status,
    string Outcome,
    string Message);
