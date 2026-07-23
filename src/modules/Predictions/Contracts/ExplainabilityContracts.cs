using BeeEye.Analytics.Explainability;
using BeeEye.Modules.Predictions.Application;
using BeeEye.Shared.Web.Idempotency;

namespace BeeEye.Modules.Predictions.Contracts;

/// <summary>One expected-impact tile. <paramref name="Value"/> is pre-formatted invariantly.</summary>
/// <param name="Tone">A tone key (<c>neutral</c>/<c>positive</c>/<c>negative</c>/<c>warning</c>),
/// never a colour — the browser owns colour.</param>
public sealed record ImpactTileDto(string Label, string Value, string Tone)
{
    public static ImpactTileDto From(ImpactTile t) =>
        new(t.Label, t.Value, ExplanationVocabulary.KeyFor(t.Tone));
}

/// <summary>The computed confidence band and the reasons behind it.</summary>
public sealed record ConfidenceDto(string Band, int? Percent, IReadOnlyList<string> Why)
{
    public static ConfidenceDto From(ConfidenceStatement c) =>
        new(c.Band.ToString(), c.Percent, c.Why);
}

/// <summary>One ranked reason behind the output.</summary>
public sealed record DriverDto(string Label, string? Detail)
{
    public static DriverDto From(Driver d) => new(d.Label, d.Detail);
}

/// <summary>One point on the historical-evidence chart.</summary>
public sealed record EvidencePointDto(string Label, decimal Value, decimal? Comparison)
{
    public static EvidencePointDto From(EvidencePoint p) => new(p.Label, p.Value, p.Comparison);
}

/// <summary>The chartable evidence behind the output.</summary>
public sealed record EvidenceSeriesDto(
    string Period,
    IReadOnlyList<EvidencePointDto> Points,
    string? Note,
    string ValueLabel,
    string? ComparisonLabel)
{
    public static EvidenceSeriesDto From(EvidenceSeries e) => new(
        e.Period,
        [.. e.Points.Select(EvidencePointDto.From)],
        e.Note,
        e.ValueLabel,
        e.ComparisonLabel);
}

/// <summary>One input the output was computed from.</summary>
/// <param name="Kind">A lineage key: <c>fusion</c>, <c>workbook</c>, <c>demo</c> or <c>derived</c>.</param>
public sealed record LineageNodeDto(string Label, string Kind)
{
    public static LineageNodeDto From(LineageNode n) =>
        new(n.Label, ExplanationVocabulary.KeyFor(n.Kind));
}

/// <summary>Model or rule-set metadata — v3's six-field grid.</summary>
public sealed record ModelInfoDto(
    string Name,
    string Version,
    string Recalculated,
    string Horizon,
    string Validation,
    string Error)
{
    public static ModelInfoDto From(ModelInfo m) =>
        new(m.Name, m.Version, m.Recalculated, m.Horizon, m.Validation, m.Error);
}

/// <summary>Who owns the decision and where it stands.</summary>
public sealed record OwnershipDto(string OwnerRole, string Status)
{
    public static OwnershipDto From(Ownership o) => new(o.OwnerRole, o.Status);
}

/// <summary>
/// The explainability payload for one subject.
/// <para>
/// Enums are projected to <b>strings</b>, matching every other contract in the platform: an ordinal
/// on the wire means renumbering an enum silently reinterprets a stored or cached payload.
/// </para>
/// </summary>
/// <param name="Label">
/// The output-label key from <c>engine2.js</c>'s <c>LABELS</c> table — one of <c>observed</c>,
/// <c>calculated</c>, <c>forecast</c>, <c>recommendation</c>, <c>simulation</c>, <c>demo</c>,
/// <c>low</c>, <c>dq</c>. Always present.
/// </param>
/// <param name="Confidence">
/// Null when the engine computed no band. <b>Not defaulted to Medium</b> — see
/// <see cref="Explanation.Confidence"/>.
/// </param>
/// <param name="IsDemoData">
/// True when the figure derives from the synthetic-demo dataset. Stated explicitly rather than
/// inferred from a <c>demo</c> lineage node, so the two can never drift apart.
/// </param>
public sealed record ExplanationDto(
    string Title,
    string Module,
    string Label,
    string? Recommendation,
    IReadOnlyList<ImpactTileDto> Impacts,
    ConfidenceDto? Confidence,
    IReadOnlyList<DriverDto> Drivers,
    EvidenceSeriesDto? Evidence,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<LineageNodeDto> Lineage,
    ModelInfoDto? Model,
    OwnershipDto? Ownership,
    bool IsDemoData)
{
    public static ExplanationDto From(Explanation e) => new(
        e.Title,
        e.Module,
        ExplanationVocabulary.KeyFor(e.Label),
        e.Recommendation,
        [.. e.Impacts.Select(ImpactTileDto.From)],
        e.Confidence is null ? null : ConfidenceDto.From(e.Confidence),
        [.. e.Drivers.Select(DriverDto.From)],
        e.Evidence is null ? null : EvidenceSeriesDto.From(e.Evidence),
        e.Assumptions,
        [.. e.Lineage.Select(LineageNodeDto.From)],
        e.Model is null ? null : ModelInfoDto.From(e.Model),
        e.Ownership is null ? null : OwnershipDto.From(e.Ownership),
        e.IsDemoData);
}

/// <summary>
/// A context that could not be reached. Reported rather than hidden: a drawer that silently dropped
/// a failed provider would read as "there is nothing to explain here", which is the opposite of true.
/// </summary>
public sealed record ExplanationGapDto(string Area, string Reason)
{
    public static ExplanationGapDto From(ExplanationGap g) => new(g.Area, g.Reason);
}

/// <summary>
/// One verdict on an explanation, as the drawer reads it back.
/// </summary>
/// <param name="SubmittedBy">A stable subject id, never a display name.</param>
public sealed record FeedbackEntryDto(
    string Verdict,
    string? Note,
    string SubmittedBy,
    DateTimeOffset SubmittedAtUtc);

/// <summary>
/// A verdict on an explanation. Implements <see cref="IIdempotentPayload"/> so the
/// <c>Idempotency-Key</c> fingerprint covers the body — a replayed key with a changed verdict is a
/// different intent and is refused rather than silently answered with the first one.
/// </summary>
/// <param name="Kind">The subject kind the explanation was about.</param>
/// <param name="Ref">The subject reference.</param>
/// <param name="Verdict">One of <c>Useful</c>, <c>NeedsReview</c>, <c>Incorrect</c>, <c>MissingContext</c>.</param>
/// <param name="Note">Optional free text, up to 1000 characters.</param>
public sealed record FeedbackRequest(
    string? Kind,
    string? Ref,
    string? Verdict,
    string? Note) : IIdempotentPayload;

/// <summary>
/// What was recorded.
/// </summary>
/// <param name="Caveat">
/// Carried on <b>every</b> response, and repeated verbatim in the drawer: feedback is recorded in this
/// analytics platform only and retrains nothing. It is on the response rather than only in the UI so
/// that any future client gets the caveat whether or not it remembers to write one.
/// </param>
public sealed record FeedbackResponse(
    string SubjectKind,
    string SubjectRef,
    string Verdict,
    DateTimeOffset SubmittedAtUtc,
    string Caveat);

/// <summary>The response of <c>GET /api/v1/predictions/explain</c>.</summary>
/// <param name="SubjectKind">Echoed back, so a cached payload identifies itself.</param>
/// <param name="SubjectRef">Echoed back.</param>
/// <param name="Explanation">Null only when every claimant failed — see <paramref name="Gaps"/>.</param>
/// <param name="Gaps">Empty on a complete answer.</param>
/// <param name="Feedback">
/// The current verdict each person has left on this explanation — latest row per submitter. Returned
/// with the explanation rather than behind a second request, so the drawer opens in one round trip and
/// a reader immediately sees their own last answer instead of an empty control.
/// </param>
/// <param name="FeedbackCaveat">
/// Repeated here as well as on the write, so a client that only ever reads still knows the feedback
/// retrains nothing.
/// </param>
/// <param name="GeneratedAtUtc">When the payload was assembled.</param>
public sealed record ExplanationResponse(
    string SubjectKind,
    string SubjectRef,
    ExplanationDto? Explanation,
    IReadOnlyList<ExplanationGapDto> Gaps,
    IReadOnlyList<FeedbackEntryDto> Feedback,
    string FeedbackCaveat,
    DateTimeOffset GeneratedAtUtc);
