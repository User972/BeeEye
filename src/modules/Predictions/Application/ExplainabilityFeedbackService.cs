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
    /// The current verdict per person for one subject — the latest row each submitter left.
    /// <para>
    /// Latest-per-submitter rather than every row: the drawer asks "what do I currently think?", and
    /// showing someone their three superseded answers beside their current one would be noise. The
    /// superseded rows are still there, which is the point of appending.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<FeedbackEntryDto>> LatestAsync(
        string subjectKind, string subjectRef, CancellationToken cancellationToken)
    {
        var rows = await db.ExplainabilityFeedback.AsNoTracking()
            .Where(f => f.SubjectKind == subjectKind && f.SubjectRef == subjectRef)
            .OrderByDescending(f => f.SubmittedAtUtc)
            .ThenByDescending(f => f.Id)
            .ToListAsync(cancellationToken);

        // Grouped in memory rather than with a window function: this is a handful of rows per
        // subject, and the read-store seam that would justify hand-written SQL is deferred (TD-1).
        return
        [
            .. rows
                .GroupBy(f => f.SubmittedBy, StringComparer.Ordinal)
                .Select(g => g.First())
                .OrderByDescending(f => f.SubmittedAtUtc)
                .Select(f => new FeedbackEntryDto(
                    f.Verdict.ToString(), f.Note, f.SubmittedBy, f.SubmittedAtUtc)),
        ];
    }

    /// <summary>Matches the column length, so a rejection happens before the database refuses it.</summary>
    public const int MaxNoteLength = 1000;
}
