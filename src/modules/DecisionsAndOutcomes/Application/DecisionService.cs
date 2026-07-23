using System.Globalization;
using System.Text.Json;
using BeeEye.Modules.DecisionsAndOutcomes.Contracts;
using BeeEye.Persistence;
using BeeEye.Persistence.Entities;
using BeeEye.Shared.Decisions;
using BeeEye.Shared.Results;
using BeeEye.Shared.Security;
using BeeEye.Shared.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BeeEye.Modules.DecisionsAndOutcomes.Application;

/// <summary>Filters for the Decision Log query.</summary>
/// <param name="Status">Lifecycle status (the chip row).</param>
/// <param name="Area">Business area that raised the recommendation.</param>
/// <param name="Outcome">The human's verdict.</param>
/// <param name="From">Earliest creation date, inclusive.</param>
/// <param name="To">Latest creation date, inclusive.</param>
/// <param name="Query">Free-text match over subject, action and rule id.</param>
public sealed record DecisionLogFilters(
    RecommendationStatus? Status = null,
    string? Area = null,
    DecisionOutcome? Outcome = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Query = null);

/// <summary>
/// The business rules that sit above the lifecycle state machine: who may claim, what a modification
/// may say, who may sign off, and what "implemented" is allowed to mean.
/// <para>
/// Every state change here goes through <see cref="RecommendationTransitionService"/>. This class owns
/// the <i>human</i> rules — segregation of duties, mandatory rejection notes, the discount bound — and
/// owns none of the transition rules, so there is exactly one copy of each.
/// </para>
/// </summary>
public sealed class DecisionService(
    BeeEyeDbContext db,
    RecommendationTransitionService transitions,
    IClock clock,
    ILogger<DecisionService> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The largest page the log will serve, however much a caller asks for.</summary>
    public const int MaxPageSize = 200;

    // ------------------------------------------------------------------ reading

    /// <summary>
    /// A page of the Decision Log, with the status counts behind the chip row.
    /// </summary>
    public async Task<DecisionLogPageDto> ListAsync(
        DecisionLogFilters filters,
        IReadOnlySet<string> permissions,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filters);

        var take = Math.Clamp(pageSize, 1, MaxPageSize);
        var skip = Math.Max(page, 1);

        // Everything except the status filter, so selecting a chip does not zero every other chip and
        // strand the user on a filter they cannot see their way out of.
        var unfiltered = ApplyFilters(db.Recommendations.AsNoTracking(), filters with { Status = null });

        var counts = await unfiltered
            .GroupBy(r => r.CurrentStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var query = filters.Status is { } status
            ? unfiltered.Where(r => r.CurrentStatus == status)
            : unfiltered;

        var total = await query.CountAsync(cancellationToken);

        // Ordered so paging is deterministic: highest priority first, newest next, and the id as a
        // total order so two records with identical scores never swap between pages.
        var recommendations = await query
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.CreatedAtUtc)
            .ThenBy(r => r.Id)
            .Skip((skip - 1) * take)
            .Take(take)
            .ToListAsync(cancellationToken);

        var ids = recommendations.Select(r => r.Id).ToList();

        var decisions = await db.ManagementDecisions
            .AsNoTracking()
            .Where(d => ids.Contains(d.RecommendationId))
            .Include(d => d.ApprovalSteps)
            .ToListAsync(cancellationToken);

        var byRecommendation = decisions
            .GroupBy(d => d.RecommendationId)
            // Newest first, so a record with history shows the decision that currently governs it.
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.OpenedAtUtc).First());

        var items = recommendations
            .Select(r =>
            {
                byRecommendation.TryGetValue(r.Id, out var decision);
                return ToLogItem(r, decision, permissions);
            })
            .ToList();

        var statusCounts = Enum.GetValues<RecommendationStatus>()
            .ToDictionary(
                s => s.ToString(),
                s => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0,
                StringComparer.Ordinal);

        return new DecisionLogPageDto(items, skip, take, total, statusCounts);
    }

    /// <summary>
    /// The frozen original beside the human decision, the approval chain, the full status-event log
    /// and the realised outcome — everything ADR 0006 promises can be read side by side.
    /// </summary>
    public async Task<Result<DecisionDetailDto>> GetDetailAsync(
        Guid recommendationId, IReadOnlySet<string> permissions, CancellationToken cancellationToken)
    {
        var recommendation = await db.Recommendations
            .AsNoTracking()
            .Include(r => r.StatusEvents)
            .SingleOrDefaultAsync(r => r.Id == recommendationId, cancellationToken);

        if (recommendation is null)
        {
            return Result.Failure<DecisionDetailDto>(Error.NotFound(NotFoundDetail));
        }

        var decision = await db.ManagementDecisions
            .AsNoTracking()
            .Include(d => d.ApprovalSteps)
            .Include(d => d.ActionOutcome)
            .Where(d => d.RecommendationId == recommendationId)
            .OrderByDescending(d => d.OpenedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var steps = decision?.ApprovalSteps
            .OrderBy(s => s.StepNumber)
            .Select(ToStepDto)
            .ToList() ?? [];

        var events = recommendation.StatusEvents
            .OrderBy(e => e.AtUtc)
            .ThenBy(e => e.Id)
            .Select(e => new StatusEventDto(
                e.FromStatus?.ToString(), e.ToStatus.ToString(), e.Actor, e.Reason, e.AtUtc))
            .ToList();

        var outcome = decision?.ActionOutcome is { } measured
            ? new ActionOutcomeDto(
                measured.Metric, measured.RealisedValue, measured.Unit,
                measured.MeasuredAtUtc, measured.RecordedBy, measured.Note)
            : null;

        return Result.Success(new DecisionDetailDto(
            ToSnapshot(recommendation),
            decision is null ? null : ToDecisionSummary(decision),
            steps,
            events,
            outcome,
            AvailableActions(recommendation, decision, permissions)));
    }

    /// <summary>
    /// The frozen recommendation, by its own id, for the explainability drawer.
    /// <para>
    /// Keyed on the recommendation's <b>unique</b> id, never its rule id: the log is append-only history
    /// holding many records under one rule id (one per generation run, each about a different subject),
    /// so a rule id explains nothing on its own. This returns the exact frozen record the drawer was
    /// opened over — or nothing, which the provider turns into a 404.
    /// </para>
    /// </summary>
    public async Task<RecommendationSnapshotDto?> GetSnapshotAsync(
        Guid recommendationId, CancellationToken cancellationToken)
    {
        var recommendation = await db.Recommendations
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == recommendationId, cancellationToken);

        return recommendation is null ? null : ToSnapshot(recommendation);
    }

    // ------------------------------------------------------------------ writing

    /// <summary>
    /// Claims a recommendation: <c>Generated → UnderReview</c>, opening a <see cref="ManagementDecision"/>
    /// and seeding the approval chain.
    /// <para>
    /// One approval step is seeded from the recommendation's owner role. Seeding it at claim time —
    /// rather than at accept time — is the safer choice: it makes the supersession guard real from the
    /// moment a human takes ownership, so a fresh analysis run cannot erase a decision mid-flight, and
    /// it means the second-person requirement is visible before anyone commits to a verdict.
    /// </para>
    /// </summary>
    public async Task<Result<TransitionResponseDto>> ClaimAsync(
        Guid recommendationId, string actor, string idempotencyKey, CancellationToken cancellationToken)
    {
        var recommendation = await db.Recommendations
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == recommendationId, cancellationToken);

        if (recommendation is null)
        {
            return Result.Failure<TransitionResponseDto>(Error.NotFound(NotFoundDetail));
        }

        // An optimisation, not the guarantee: two callers can both read "unclaimed". The filtered
        // unique index below is what actually decides, exactly as S5's generation key does.
        var alreadyOpen = await db.ManagementDecisions
            .AsNoTracking()
            .AnyAsync(
                d => d.RecommendationId == recommendationId && d.Outcome == DecisionOutcome.Open,
                cancellationToken);

        if (alreadyOpen)
        {
            return Result.Failure<TransitionResponseDto>(Error.Conflict(AlreadyClaimedDetail));
        }

        var now = clock.UtcNow;
        var decision = new ManagementDecision
        {
            Id = Guid.CreateVersion7(),
            RecommendationId = recommendationId,
            IdempotencyKey = idempotencyKey,
            OpenedBy = actor,
            OpenedAtUtc = now,
            Outcome = DecisionOutcome.Open,
        };

        decision.ApprovalSteps.Add(new ApprovalStep
        {
            Id = Guid.CreateVersion7(),
            DecisionId = decision.Id,
            StepNumber = 1,
            ApproverRole = string.IsNullOrWhiteSpace(recommendation.OwnerRole)
                ? "Approver"
                : recommendation.OwnerRole,
            Status = ApprovalStepStatus.Pending,
        });

        db.ManagementDecisions.Add(decision);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (PostgresErrors.IsUniqueViolation(ex))
        {
            // Someone claimed it between our check and our write. A conflict the caller can act on,
            // never a 500 — the system behaved exactly as designed.
            logger.LogInformation(
                ex, "Concurrent claim on recommendation {RecommendationId}; this attempt was refused.",
                recommendationId);

            db.ChangeTracker.Clear();
            return Result.Failure<TransitionResponseDto>(Error.Conflict(AlreadyClaimedDetail));
        }

        var moved = await transitions.ApplyAsync(
            recommendationId, RecommendationStatus.UnderReview, actor,
            "Claimed for review.", cancellationToken);

        if (moved.IsFailure)
        {
            return Result.Failure<TransitionResponseDto>(moved.Error);
        }

        return Result.Success(new TransitionResponseDto(
            recommendationId,
            decision.Id,
            RecommendationStatus.UnderReview.ToString(),
            DecisionOutcome.Open.ToString(),
            "You now own this recommendation. It will not expire while it is under review."));
    }

    /// <summary>Accepts the recommendation exactly as the engine wrote it.</summary>
    public Task<Result<TransitionResponseDto>> AcceptAsync(
        Guid decisionId, string actor, CancellationToken cancellationToken) =>
        DecideAsync(
            decisionId,
            actor,
            RecommendationStatus.Accepted,
            DecisionOutcome.Accepted,
            note: null,
            modification: null,
            reason: "Accepted as recommended.",
            message: "Accepted as recommended. The original recommendation is unchanged.",
            cancellationToken);

    /// <summary>
    /// Accepts with a change. The recommendation row is <b>never</b> touched: the delta is stored on
    /// the decision so the engine's value and the human's stay readable beside each other.
    /// </summary>
    public async Task<Result<TransitionResponseDto>> AcceptWithModificationAsync(
        Guid decisionId, string actor, ModificationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var loaded = await LoadForDecisionAsync(decisionId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<TransitionResponseDto>(loaded.Error);
        }

        var (_, recommendation) = loaded.Value;

        var modification = new Modification(
            request.Field?.Trim() ?? string.Empty, request.From, request.To, Blank(request.Rationale));

        // Verified against the engine's own value where the frozen record states it; skipped where it
        // does not, rather than guessed at.
        decimal? recommended = RecommendedValues.TryDerive(recommendation, modification.Field, out var engineValue)
            ? engineValue
            : null;

        var verdict = ModificationRules.Validate(modification, recommended);
        if (!verdict.Valid)
        {
            return Result.Failure<TransitionResponseDto>(ModificationError(verdict));
        }

        return await DecideAsync(
            decisionId,
            actor,
            RecommendationStatus.AcceptedModified,
            DecisionOutcome.AcceptedModified,
            note: modification.Rationale,
            modification: modification,
            reason: "Accepted with modification — " + modification.Describe(),
            message:
                "Accepted with modification. The recommendation itself is unchanged; your change is "
                + "recorded beside it as " + modification.Describe() + ".",
            cancellationToken);
    }

    /// <summary>Declines the recommendation. The note is mandatory and is retained on the record.</summary>
    public async Task<Result<TransitionResponseDto>> RejectAsync(
        Guid decisionId, string actor, string? note, CancellationToken cancellationToken)
    {
        var trimmed = Blank(note);

        // Deliberately routed through the state machine rather than checked here, so the caller reads
        // the wording the machine already owns and there is one rule, not two.
        if (trimmed is null)
        {
            var refusal = RecommendationLifecycle.CanTransition(
                RecommendationStatus.UnderReview, RecommendationStatus.Rejected, hasReason: false);

            return Result.Failure<TransitionResponseDto>(Error.Validation(refusal.Explain()));
        }

        return await DecideAsync(
            decisionId,
            actor,
            RecommendationStatus.Rejected,
            DecisionOutcome.Rejected,
            note: trimmed,
            modification: null,
            reason: trimmed,
            message: "Rejected. This is a final state — the record stays readable, with your reason attached.",
            cancellationToken);
    }

    /// <summary>
    /// Signs off one step of the approval chain.
    /// <para>
    /// <b>Segregation of duties, layer two.</b> Permissions keep an author from approving their own
    /// class of work; this keeps a specific person from approving their <i>own</i> decision. A decision
    /// needs a second human, and comparing stable subject ids is what makes that true rather than
    /// aspirational.
    /// </para>
    /// </summary>
    public async Task<Result<TransitionResponseDto>> SignOffAsync(
        Guid decisionId, int stepNumber, string actor, bool approved, string? note,
        CancellationToken cancellationToken)
    {
        var decision = await db.ManagementDecisions
            .Include(d => d.ApprovalSteps)
            .SingleOrDefaultAsync(d => d.Id == decisionId, cancellationToken);

        if (decision is null)
        {
            return Result.Failure<TransitionResponseDto>(Error.NotFound(NotFoundDetail));
        }

        var step = decision.ApprovalSteps.SingleOrDefault(s => s.StepNumber == stepNumber);
        if (step is null)
        {
            return Result.Failure<TransitionResponseDto>(
                Error.NotFound("That approval step does not exist on this decision."));
        }

        if (step.Status != ApprovalStepStatus.Pending)
        {
            // Already acted on, and a step is immutable once acted on. Overwriting would erase who
            // actually approved — the one fact the chain exists to record.
            return Result.Failure<TransitionResponseDto>(Error.Conflict(
                $"This approval step was already {step.Status.ToString().ToLowerInvariant()} and cannot be changed."));
        }

        if (SubjectIds.Same(actor, decision.DecidedBy ?? decision.OpenedBy))
        {
            return Result.Failure<TransitionResponseDto>(Error.Forbidden(
                "A decision needs a second person: the same user cannot both make it and approve it."));
        }

        var now = clock.UtcNow;
        step.Status = approved ? ApprovalStepStatus.Approved : ApprovalStepStatus.Declined;
        step.ActedBy = actor;
        step.ActedAtUtc = now;
        step.Note = Blank(note);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return Result.Failure<TransitionResponseDto>(Error.Conflict(StaleDetail));
        }

        var recommendation = await db.Recommendations
            .AsNoTracking()
            .SingleAsync(r => r.Id == decision.RecommendationId, cancellationToken);

        return Result.Success(new TransitionResponseDto(
            decision.RecommendationId,
            decision.Id,
            recommendation.CurrentStatus.ToString(),
            decision.Outcome.ToString(),
            approved
                ? "Approval recorded. The recommendation's lifecycle status is unchanged by a sign-off."
                : "Decline recorded. This decision cannot progress to implemented while a step is declined."));
    }

    /// <summary>
    /// Records that a human executed the action downstream.
    /// <para>
    /// <b>BeeEye never writes to Oracle Fusion</b> (ADR 0006 §7). Reaching <c>Implemented</c> confirms
    /// that a person carried the action out in the enterprise systems and said so — it is not, and must
    /// never be presented as, evidence that a transaction was posted.
    /// </para>
    /// </summary>
    public async Task<Result<TransitionResponseDto>> MarkImplementedAsync(
        Guid decisionId, string actor, CancellationToken cancellationToken)
    {
        var decision = await db.ManagementDecisions
            .Include(d => d.ApprovalSteps)
            .SingleOrDefaultAsync(d => d.Id == decisionId, cancellationToken);

        if (decision is null)
        {
            return Result.Failure<TransitionResponseDto>(Error.NotFound(NotFoundDetail));
        }

        if (decision.ApprovalSteps.Any(s => s.Status == ApprovalStepStatus.Declined))
        {
            return Result.Failure<TransitionResponseDto>(Error.Conflict(
                "An approver declined a step on this decision, so it cannot be marked as implemented."));
        }

        var now = clock.UtcNow;
        decision.ImplementedBy = actor;
        decision.ImplementedAtUtc = now;

        var moved = await transitions.ApplyAsync(
            decision.RecommendationId,
            RecommendationStatus.Implemented,
            actor,
            "Confirmed executed in the enterprise systems by a human. BeeEye wrote nothing to Oracle Fusion.",
            cancellationToken);

        if (moved.IsFailure)
        {
            db.ChangeTracker.Clear();
            return Result.Failure<TransitionResponseDto>(moved.Error);
        }

        return Result.Success(new TransitionResponseDto(
            decision.RecommendationId,
            decision.Id,
            RecommendationStatus.Implemented.ToString(),
            decision.Outcome.ToString(),
            "Recorded as implemented. This confirms a person executed the action in the enterprise "
            + "systems — BeeEye does not write to Oracle Fusion."));
    }

    /// <summary>
    /// Records the realised effect, closing the learning loop.
    /// <para>
    /// The recorder <b>may</b> be the same person who decided. Measuring what actually happened is
    /// observation, not a second approval, and requiring a third party would mean outcomes simply never
    /// get captured — losing the one signal that tells ADMC whether the recommendations were any good.
    /// </para>
    /// </summary>
    public async Task<Result<TransitionResponseDto>> RecordOutcomeAsync(
        Guid decisionId, string actor, OutcomeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Metric))
        {
            return Result.Failure<TransitionResponseDto>(
                Error.Validation("Name the metric that was measured, e.g. 'Holding cost avoided'."));
        }

        var decision = await db.ManagementDecisions
            .Include(d => d.ActionOutcome)
            .SingleOrDefaultAsync(d => d.Id == decisionId, cancellationToken);

        if (decision is null)
        {
            return Result.Failure<TransitionResponseDto>(Error.NotFound(NotFoundDetail));
        }

        if (decision.ActionOutcome is not null)
        {
            return Result.Failure<TransitionResponseDto>(Error.Conflict(
                "An outcome has already been recorded for this decision."));
        }

        var metric = request.Metric.Trim();
        var unit = Blank(request.Unit);

        db.ActionOutcomes.Add(new ActionOutcome
        {
            Id = Guid.CreateVersion7(),
            DecisionId = decision.Id,
            Metric = metric,
            RealisedValue = request.RealisedValue,
            Unit = unit,
            MeasuredAtUtc = clock.UtcNow,
            RecordedBy = actor,
            Note = Blank(request.Note),
        });

        // Invariant culture: the reason text is a stored audit record, so a comma-decimal server
        // locale must not change what it says a year from now.
        var reason = string.Create(
            CultureInfo.InvariantCulture,
            $"Outcome measured: {metric} = {request.RealisedValue}{(unit is null ? string.Empty : " " + unit)}");

        var moved = await transitions.ApplyAsync(
            decision.RecommendationId,
            RecommendationStatus.OutcomeRecorded,
            actor,
            reason,
            cancellationToken);

        if (moved.IsFailure)
        {
            db.ChangeTracker.Clear();
            return Result.Failure<TransitionResponseDto>(moved.Error);
        }

        return Result.Success(new TransitionResponseDto(
            decision.RecommendationId,
            decision.Id,
            RecommendationStatus.OutcomeRecorded.ToString(),
            decision.Outcome.ToString(),
            "Outcome recorded. This recommendation's lifecycle is complete."));
    }

    // ------------------------------------------------------------------ internals

    private const string NotFoundDetail = "That record could not be found.";

    private const string AlreadyClaimedDetail =
        "Someone else is already reviewing this recommendation. Open the Decision Log to see who has it.";

    private const string StaleDetail =
        "Someone else updated this decision while you were working on it. Reload it and try again.";

    /// <summary>
    /// The shared body of accept / accept-with-modification / reject: stamp the human's verdict onto
    /// the decision and move the recommendation, in that order and in one save.
    /// </summary>
    private async Task<Result<TransitionResponseDto>> DecideAsync(
        Guid decisionId,
        string actor,
        RecommendationStatus to,
        DecisionOutcome outcome,
        string? note,
        Modification? modification,
        string reason,
        string message,
        CancellationToken cancellationToken)
    {
        var decision = await db.ManagementDecisions
            .SingleOrDefaultAsync(d => d.Id == decisionId, cancellationToken);

        if (decision is null)
        {
            return Result.Failure<TransitionResponseDto>(Error.NotFound(NotFoundDetail));
        }

        if (decision.Outcome != DecisionOutcome.Open)
        {
            return Result.Failure<TransitionResponseDto>(Error.Conflict(
                $"This decision was already recorded as {Humanise(decision.Outcome)} and cannot be decided again."));
        }

        decision.Outcome = outcome;
        decision.DecidedBy = actor;
        decision.DecidedAtUtc = clock.UtcNow;
        decision.Note = note ?? decision.Note;
        decision.ModificationJson = modification is null
            ? null
            : JsonSerializer.Serialize(modification, Json);

        var moved = await transitions.ApplyAsync(
            decision.RecommendationId, to, actor, reason, cancellationToken);

        if (moved.IsFailure)
        {
            // The transition service already rolled its own change back; drop ours too, so a refused
            // move never leaves a decision claiming a verdict the lifecycle does not reflect.
            db.ChangeTracker.Clear();
            return Result.Failure<TransitionResponseDto>(moved.Error);
        }

        return Result.Success(new TransitionResponseDto(
            decision.RecommendationId, decision.Id, to.ToString(), outcome.ToString(), message));
    }

    private async Task<Result<(ManagementDecision Decision, Recommendation Recommendation)>> LoadForDecisionAsync(
        Guid decisionId, CancellationToken cancellationToken)
    {
        var decision = await db.ManagementDecisions
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == decisionId, cancellationToken);

        if (decision is null)
        {
            return Result.Failure<(ManagementDecision, Recommendation)>(Error.NotFound(NotFoundDetail));
        }

        var recommendation = await db.Recommendations
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == decision.RecommendationId, cancellationToken);

        return recommendation is null
            ? Result.Failure<(ManagementDecision, Recommendation)>(Error.NotFound(NotFoundDetail))
            : Result.Success((decision, recommendation));
    }

    private IQueryable<Recommendation> ApplyFilters(
        IQueryable<Recommendation> query, DecisionLogFilters filters)
    {
        if (filters.Status is { } status)
        {
            query = query.Where(r => r.CurrentStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(filters.Area))
        {
            var area = filters.Area.Trim();
            query = query.Where(r => r.Area == area);
        }

        if (filters.Outcome is { } outcome)
        {
            // Filtered by the human verdict rather than the lifecycle status: "which recommendations
            // did we reject" and "which are in the Rejected state" are the same question today, but
            // they are different questions, and the log answers both.
            query = query.Where(r =>
                db.ManagementDecisions.Any(d => d.RecommendationId == r.Id && d.Outcome == outcome));
        }

        if (filters.From is { } from)
        {
            query = query.Where(r => r.CreatedAtUtc >= from);
        }

        if (filters.To is { } to)
        {
            query = query.Where(r => r.CreatedAtUtc <= to);
        }

        if (!string.IsNullOrWhiteSpace(filters.Query))
        {
            var term = filters.Query.Trim();
            query = query.Where(r =>
                EF.Functions.ILike(r.SubjectRef, $"%{term}%")
                || EF.Functions.ILike(r.Action, $"%{term}%")
                || EF.Functions.ILike(r.RuleId, $"%{term}%"));
        }

        return query;
    }

    private DecisionLogItemDto ToLogItem(
        Recommendation r, ManagementDecision? decision, IReadOnlySet<string> permissions) =>
        new(
            r.Id,
            decision?.Id,
            r.RuleId,
            r.SubjectRef,
            r.Area,
            r.Action,
            FirstEvidence(r.EvidenceJson),
            r.CurrentStatus.ToString(),
            decision?.Outcome.ToString(),
            r.ImpactSar,
            r.Priority,
            r.OwnerRole,
            r.IsDemoData,
            $"Rule {r.RuleId} · ruleset {r.RulesetVersion}",
            r.CreatedAtUtc,
            decision?.DecidedBy,
            decision?.DecidedAtUtc,
            ReadModification(decision?.ModificationJson),
            AvailableActions(r, decision, permissions));

    private static IReadOnlyList<string> AvailableActions(
        Recommendation r, ManagementDecision? decision, IReadOnlySet<string> permissions) =>
        DecisionActions.For(
            r.CurrentStatus,
            decision?.Outcome,
            decision?.ApprovalSteps.Any(s => s.Status == ApprovalStepStatus.Pending) == true,
            permissions);

    private static DecisionSummaryDto ToDecisionSummary(ManagementDecision d) =>
        new(
            d.Id,
            d.Outcome.ToString(),
            d.OpenedBy,
            d.OpenedAtUtc,
            d.DecidedBy,
            d.DecidedAtUtc,
            d.Note,
            ReadModification(d.ModificationJson),
            d.ImplementedBy,
            d.ImplementedAtUtc);

    private static ApprovalStepDto ToStepDto(ApprovalStep s) =>
        new(s.StepNumber, s.ApproverRole, s.Status.ToString(), s.ActedBy, s.ActedAtUtc, s.Note);

    private static RecommendationSnapshotDto ToSnapshot(Recommendation r) =>
        new(
            r.Id, r.RuleId, r.SubjectRef, r.Area, r.Action, r.Rationale,
            ReadStrings(r.EvidenceJson), r.ExpectedOutcome, r.Confidence, ReadStrings(r.AssumptionsJson),
            r.ImpactSar, r.Priority, r.OwnerRole, r.IsDemoData,
            r.RulesetVersion, r.DatasetVersion, r.AnalysisDate,
            r.CurrentStatus.ToString(), r.ValidUntilUtc, r.SupersededByRecommendationId, r.CreatedAtUtc);

    private static Error ModificationError(ModificationValidation verdict) => verdict.Refusal switch
    {
        // Well-formed but unacceptable content: a bound the business set, or a stale original value.
        ModificationRefusal.DiscountOutOfRange or ModificationRefusal.StaleOriginalValue =>
            Error.Unprocessable(verdict.Explain()),

        // A malformed request the caller can correct.
        _ => Error.Validation(verdict.Explain()),
    };

    private static string Humanise(DecisionOutcome outcome) => outcome switch
    {
        DecisionOutcome.AcceptedModified => "accepted with modification",
        _ => outcome.ToString().ToLowerInvariant(),
    };

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ModificationDto? ReadModification(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var modification = JsonSerializer.Deserialize<Modification>(json, Json);
            return modification is null
                ? null
                : new ModificationDto(modification.Field, modification.From, modification.To, modification.Rationale);
        }
        catch (JsonException)
        {
            // Written by this application, so unreadable means corrupted. Degrading to "no
            // modification shown" is wrong enough to hide, so it degrades to null and the detail view
            // simply omits it rather than failing the whole page.
            return null;
        }
    }

    private static IReadOnlyList<string> ReadStrings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string FirstEvidence(string? json) => ReadStrings(json).FirstOrDefault() ?? string.Empty;
}
