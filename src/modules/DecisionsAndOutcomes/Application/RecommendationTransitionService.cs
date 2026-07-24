using BeeEye.Persistence;
using BeeEye.Persistence.Entities;
using BeeEye.Shared.Decisions;
using BeeEye.Shared.Results;
using BeeEye.Shared.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BeeEye.Modules.DecisionsAndOutcomes.Application;

/// <summary>A transition that was applied, with the event that recorded it.</summary>
/// <param name="RecommendationId">The recommendation that moved.</param>
/// <param name="From">The state it was in.</param>
/// <param name="To">The state it is now in.</param>
/// <param name="Actor">The stable subject id that caused the move, or "system".</param>
/// <param name="AtUtc">When it happened.</param>
public sealed record TransitionApplied(
    Guid RecommendationId,
    RecommendationStatus From,
    RecommendationStatus To,
    string Actor,
    DateTimeOffset AtUtc);

/// <summary>
/// The <b>only</b> writer of recommendation lifecycle state in the platform
/// (<c>docs/adr/0006-recommendation-decision-workflow.md</c> §6: "All writers must go through the
/// transition service; no direct status column updates are allowed.").
/// <para>
/// Two properties make that worth enforcing. First, every change is validated by
/// <see cref="RecommendationLifecycle"/> and by nothing else — there is no second copy of the rules to
/// drift out of step with the first. Second, the appended status event and the
/// <see cref="Recommendation.CurrentStatus"/> projection are written in one
/// <c>SaveChangesAsync</c>, so the cached column can never disagree with the log that is the source of
/// truth.
/// </para>
/// </summary>
public sealed class RecommendationTransitionService(
    BeeEyeDbContext db,
    IClock clock,
    ILogger<RecommendationTransitionService> logger)
{
    /// <summary>The actor recorded for engine- and job-driven transitions.</summary>
    public const string SystemActor = "system";

    /// <summary>
    /// Validates and applies a transition, appending the status event that records it.
    /// </summary>
    /// <param name="recommendationId">The recommendation to move.</param>
    /// <param name="to">The proposed next state.</param>
    /// <param name="actor">Stable subject id of whoever caused it — never a display name.</param>
    /// <param name="reason">Why. Required to reject; retained on every transition that supplies one.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<Result<TransitionApplied>> ApplyAsync(
        Guid recommendationId,
        RecommendationStatus to,
        string actor,
        string? reason,
        CancellationToken cancellationToken)
    {
        // Tracked, not AsNoTracking: the xmin token must participate, so a writer working from a stale
        // read loses the save rather than silently overwriting a transition someone else made.
        var recommendation = await db.Recommendations
            .SingleOrDefaultAsync(r => r.Id == recommendationId, cancellationToken);

        if (recommendation is null)
        {
            return Result.Failure<TransitionApplied>(
                Error.NotFound("That recommendation could not be found."));
        }

        var approvalInFlight = await HasApprovalInFlightAsync(recommendationId, cancellationToken);
        var from = recommendation.CurrentStatus;

        var verdict = RecommendationLifecycle.CanTransition(
            from,
            to,
            approvalInFlight,
            hasReason: !string.IsNullOrWhiteSpace(reason));

        if (!verdict.Allowed)
        {
            // Mapped to HTTP in exactly one place, by the endpoint layer. The refusal reason travels
            // as a code so that mapping stays a lookup rather than string matching.
            return Result.Failure<TransitionApplied>(RefusalToError(verdict));
        }

        var now = clock.UtcNow;

        recommendation.CurrentStatus = to;
        db.RecommendationStatusEvents.Add(new RecommendationStatusEvent
        {
            Id = Guid.CreateVersion7(),
            RecommendationId = recommendation.Id,
            FromStatus = from,
            ToStatus = to,
            Actor = actor,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            AtUtc = now,
        });

        try
        {
            // One save: the appended event and the projection it projects are never separately
            // durable, so a crash cannot leave the column claiming something the log does not say.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // A stale xmin — someone else transitioned this recommendation between our read and our
            // write. Their transition stands; ours is refused so the log stays a true sequence.
            logger.LogInformation(
                ex,
                "Concurrent transition on recommendation {RecommendationId}; this attempt was refused.",
                recommendationId);

            db.ChangeTracker.Clear();

            return Result.Failure<TransitionApplied>(Error.Conflict(
                "Someone else updated this recommendation while you were working on it. "
                + "Reload it to see the current state, then try again."));
        }

        return Result.Success(new TransitionApplied(recommendation.Id, from, to, actor, now));
    }

    /// <summary>
    /// True when an approval step is still pending on this recommendation's open decision.
    /// <para>
    /// This is the real input to ADR 0006 §3's supersession guard. S5 could only exercise it with a
    /// synthetic flag because no approval chain existed yet; from S6 the guard reads the actual chain,
    /// so a newer analysis run cannot erase a decision a human is part-way through approving.
    /// </para>
    /// </summary>
    public Task<bool> HasApprovalInFlightAsync(Guid recommendationId, CancellationToken cancellationToken) =>
        db.ApprovalSteps
            .AsNoTracking()
            .AnyAsync(
                s => s.Status == ApprovalStepStatus.Pending
                     && db.ManagementDecisions.Any(d =>
                         d.Id == s.DecisionId
                         && d.RecommendationId == recommendationId
                         && d.Outcome == DecisionOutcome.Open),
                cancellationToken);

    /// <summary>
    /// Turns a state-machine refusal into a transport-neutral error, carrying the machine's own
    /// explanation rather than a second wording of the same rule.
    /// </summary>
    private static Error RefusalToError(TransitionResult verdict) => verdict.Refusal switch
    {
        // A missing reason is a malformed request: the caller can fix it and retry.
        TransitionRefusal.ReasonRequired => Error.Validation(verdict.Explain()),

        // Everything else is a state conflict: the request is well-formed, but the record is not
        // where the caller thinks it is.
        _ => Error.Conflict(verdict.Explain()),
    };
}
