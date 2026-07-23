using BeeEye.Modules.Predictions.Contracts;
using BeeEye.Persistence;
using BeeEye.Persistence.Entities;
using BeeEye.Shared.Results;
using BeeEye.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace BeeEye.Modules.Predictions.Application;

/// <summary>
/// Records and reads what people thought of an explanation (V3-DS-006's "Was this useful?").
/// <para>
/// <b>Append-only, and it retrains nothing.</b> Changing a verdict appends a row; nothing is updated
/// in place and no delete path exists at any layer. No model consumes this table — the response and
/// the drawer's caption both say so, and they must keep saying so.
/// </para>
/// </summary>
public sealed class ExplainabilityFeedbackService(
    BeeEyeDbContext db,
    ExplainabilityService explainability,
    IClock clock)
{
    /// <summary>The caveat carried on every write. Kept beside the write so it cannot drift from it.</summary>
    public const string RetrainingCaveat =
        "Recorded in this analytics platform only. It does not retrain any model and does not change "
        + "any recommendation.";

    /// <summary>
    /// Appends one verdict.
    /// </summary>
    /// <remarks>
    /// The <c>Idempotency-Key</c> is enforced by the endpoint filter <i>and</i> by a unique index on
    /// the column: the filter is the fast path, the index is the guarantee. Same reasoning S5 applied
    /// to the generation key and S6 to the claim key.
    /// </remarks>
    public async Task<Result<FeedbackResponse>> SubmitAsync(
        FeedbackRequest request,
        string submittedBy,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var kind = request.Kind?.Trim() ?? string.Empty;
        var subjectRef = request.Ref?.Trim() ?? string.Empty;

        if (kind.Length == 0 || subjectRef.Length == 0)
        {
            return Result<FeedbackResponse>.Failure(new Error(
                "invalid",
                "Both 'kind' and 'ref' are required — feedback has to be about something."));
        }

        if (!explainability.RegisteredKinds.Contains(kind, StringComparer.Ordinal))
        {
            return Result<FeedbackResponse>.Failure(new Error(
                "invalid",
                $"'{kind}' is not explainable, so there is nothing to give feedback on. Valid values: "
                + string.Join(", ", explainability.RegisteredKinds) + "."));
        }

        if (!Enum.TryParse<FeedbackVerdict>(request.Verdict, ignoreCase: true, out var verdict))
        {
            return Result<FeedbackResponse>.Failure(new Error(
                "invalid",
                $"'{request.Verdict}' is not a recognised verdict. Valid values: "
                + string.Join(", ", Enum.GetNames<FeedbackVerdict>()) + "."));
        }

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is { Length: > MaxNoteLength })
        {
            return Result<FeedbackResponse>.Failure(new Error(
                "invalid",
                $"The note is longer than {MaxNoteLength} characters. Shorten it and try again."));
        }

        var row = new ExplainabilityFeedback
        {
            // v7 so insertion order is readable from the key itself, the same as every other record
            // this platform appends.
            Id = Guid.CreateVersion7(),
            SubjectKind = kind,
            SubjectRef = subjectRef,
            Verdict = verdict,
            Note = note,
            SubmittedBy = submittedBy,
            SubmittedAtUtc = clock.UtcNow,
            IdempotencyKey = idempotencyKey,
        };

        db.ExplainabilityFeedback.Add(row);
        await db.SaveChangesAsync(cancellationToken);

        return Result<FeedbackResponse>.Success(new FeedbackResponse(
            kind,
            subjectRef,
            verdict.ToString(),
            row.SubmittedAtUtc,
            RetrainingCaveat));
    }

    /// <summary>
    /// The caller's <b>own</b> current verdict on one subject — the latest row they left, or nothing.
    /// <para>
    /// Deliberately scoped to the caller rather than every submitter. A verdict carries a candid note
    /// ("this safety-stock assumption is wrong") and a stable subject id; the drawer only ever surfaces
    /// the reader's own answer, so returning colleagues' notes on the wire would disclose them to every
    /// viewer for no feature that consumes them. The superseded rows are still there — that is the point
    /// of appending — but "what do I currently think?" is a question about one person, and this answers
    /// exactly that person.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<FeedbackEntryDto>> MineAsync(
        string subjectKind, string subjectRef, string submittedBy, CancellationToken cancellationToken)
    {
        // An unidentifiable caller has no "own" verdict to read back. Attribution is required to write
        // (TryActor on the feedback endpoint), so this only happens on a read by an anonymous principal
        // in a relaxed posture — which should see an empty control, not someone else's.
        if (string.IsNullOrEmpty(submittedBy))
        {
            return [];
        }

        var latest = await db.ExplainabilityFeedback.AsNoTracking()
            .Where(f => f.SubjectKind == subjectKind && f.SubjectRef == subjectRef && f.SubmittedBy == submittedBy)
            .OrderByDescending(f => f.SubmittedAtUtc)
            .ThenByDescending(f => f.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return latest is null
            ? []
            : [new FeedbackEntryDto(latest.Verdict.ToString(), latest.Note, latest.SubmittedBy, latest.SubmittedAtUtc)];
    }

    /// <summary>Matches the column length, so a rejection happens before the database refuses it.</summary>
    public const int MaxNoteLength = 1000;
}
