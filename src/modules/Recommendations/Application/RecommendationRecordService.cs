using System.Globalization;
using System.Text.Json;
using BeeEye.Analytics.Decisions;
using BeeEye.Persistence;
using BeeEye.Persistence.Entities;
using BeeEye.Shared.Decisions;
using BeeEye.Shared.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BeeEye.Modules.Recommendations.Application;

/// <summary>The result of a generation run.</summary>
/// <param name="Created">Records written by this run.</param>
/// <param name="AlreadyPresent">Candidates that already had a record — the idempotent no-op path.</param>
/// <param name="AnalysisDate">The pinned analysis date the run was computed against.</param>
public sealed record GenerationResult(int Created, int AlreadyPresent, DateOnly AnalysisDate);

/// <summary>
/// Persists engine recommendations as frozen, append-only business records
/// (<c>docs/adr/0006-recommendation-decision-workflow.md</c>).
/// <para>
/// This is the platform's first write path. Three properties matter more than anything else here:
/// </para>
/// <list type="number">
/// <item>
/// <b>The original is immutable.</b> A record is inserted once and never updated by this service.
/// A later run supersedes; it does not overwrite.
/// </item>
/// <item>
/// <b>Status lives in an append-only log.</b> Every record gets an initial <c>Generated</c> event.
/// <see cref="Recommendation.CurrentStatus"/> is only ever a projection of that log.
/// </item>
/// <item>
/// <b>Generation is idempotent.</b> The key is derived from the subject, ruleset version and analysis
/// date, and is enforced by a unique index — so a retry, a double submission or a replayed message
/// cannot create a duplicate business record (ADR 0007).
/// </item>
/// </list>
/// </summary>
public sealed class RecommendationRecordService(
    IEnumerable<IDecisionSignalProvider> providers,
    BeeEyeDbContext db,
    IClock clock,
    ILogger<RecommendationRecordService> logger)
{
    /// <summary>
    /// Version of the rule set that produces these recommendations. Bumped whenever the rules change,
    /// so a record always names the logic that produced it — and so a changed ruleset generates a
    /// fresh record rather than colliding with the old one.
    /// </summary>
    public const string RulesetVersion = "v1";

    /// <summary>How long a generated recommendation stays valid before it may expire unreviewed.</summary>
    public static readonly TimeSpan ValidityWindow = TimeSpan.FromDays(30);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Runs every context's ruleset and persists any recommendation not already recorded for this
    /// subject, ruleset and analysis date.
    /// </summary>
    public async Task<GenerationResult> GenerateAsync(
        DateOnly analysisDate, string datasetVersion, CancellationToken cancellationToken)
    {
        var candidates = await CollectAsync(cancellationToken);

        var created = 0;
        var alreadyPresent = 0;
        var now = clock.UtcNow;

        // One lookup for the whole run rather than one per candidate: the common path is a re-run
        // where every key already exists, and that must not cost a round trip each.
        var keys = candidates
            .Select(c => IdempotencyKey(c, analysisDate))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var present = new HashSet<string>(
            await db.Recommendations.AsNoTracking()
                .Where(r => keys.Contains(r.IdempotencyKey))
                .Select(r => r.IdempotencyKey)
                .ToListAsync(cancellationToken),
            StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            var key = IdempotencyKey(candidate, analysisDate);

            // `present` also absorbs duplicates *within* one run: two providers emitting the same
            // rule id and subject would otherwise collide on the unique index at save time.
            if (!present.Add(key))
            {
                alreadyPresent++;
                continue;
            }

            var recommendation = new Recommendation
            {
                Id = Guid.CreateVersion7(),
                IdempotencyKey = key,
                SubjectRef = candidate.Title,
                Area = candidate.Area,
                RuleId = candidate.Id,
                Action = candidate.Action,
                Rationale = candidate.WhyNow,
                EvidenceJson = JsonSerializer.Serialize(new[] { candidate.Evidence }, Json),
                ExpectedOutcome = candidate.Action,
                Confidence = candidate.Confidence,
                AssumptionsJson = JsonSerializer.Serialize(
                    candidate.IsDemo
                        ? new[] { "Derived from synthetic demo data, not Oracle Fusion." }
                        : Array.Empty<string>(),
                    Json),
                ImpactSar = candidate.ImpactSar,
                Priority = candidate.Priority,
                OwnerRole = candidate.OwnerRole,
                IsDemoData = candidate.IsDemo,
                RulesetVersion = RulesetVersion,
                DatasetVersion = datasetVersion,
                AnalysisDate = analysisDate,
                CurrentStatus = RecommendationLifecycle.InitialStatus,
                ValidUntilUtc = now.Add(ValidityWindow),
                CreatedAtUtc = now,
            };

            // The opening entry of the append-only log. Actor is the system: no human has yet acted,
            // and pretending otherwise would corrupt the accountability trail ADR 0006 exists for.
            recommendation.StatusEvents.Add(new RecommendationStatusEvent
            {
                Id = Guid.CreateVersion7(),
                RecommendationId = recommendation.Id,
                FromStatus = null,
                ToStatus = RecommendationLifecycle.InitialStatus,
                Actor = SystemActor,
                Reason = "Generated by the rule engine.",
                AtUtc = now,
            });

            db.Recommendations.Add(recommendation);

            // Saved one at a time, deliberately. A batched save is a single transaction, so one
            // candidate losing a race to a concurrent run would roll back every *other* record in
            // the batch — silently dropping genuinely new recommendations and reporting them as an
            // idempotent no-op. Runs produce a handful of candidates, so the extra round trips cost
            // little beside losing a decision.
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                created++;
            }
            catch (DbUpdateException ex) when (PostgresErrors.IsUniqueViolation(ex))
            {
                // A concurrent run recorded this same recommendation first. The unique index is the
                // real guarantee; losing the race is a successful no-op, not an error.
                logger.LogInformation(
                    ex,
                    "A concurrent generation run already recorded {RuleId} for {AnalysisDate}; treating as idempotent.",
                    candidate.Id,
                    analysisDate);

                Detach(recommendation);
                alreadyPresent++;
            }
        }

        return new GenerationResult(created, alreadyPresent, analysisDate);
    }

    /// <summary>The actor recorded for engine- and job-driven transitions.</summary>
    public const string SystemActor = "system";

    /// <summary>
    /// Resolves the analysis context from the data itself: the last month with sales, and the newest
    /// completed ingestion batch. Anchoring to data rather than the wall clock keeps a run reproducible
    /// — the same database always yields the same context, and therefore the same idempotency keys.
    /// </summary>
    public async Task<(DateOnly AnalysisDate, string DatasetVersion)> ResolveContextAsync(
        CancellationToken cancellationToken)
    {
        var latestMonth = await db.SalesFacts
            .AsNoTracking()
            .OrderByDescending(f => f.SaleMonth)
            .Select(f => (DateOnly?)f.SaleMonth)
            .FirstOrDefaultAsync(cancellationToken);

        // Last day of the latest month with sales; falls back to the clock only on an empty database.
        var analysisDate = latestMonth is { } month
            ? new DateOnly(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month))
            : DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        var datasetVersion = await db.IngestionBatches
            .AsNoTracking()
            .OrderByDescending(x => x.CompletedAtUtc ?? x.StartedAtUtc)
            .Select(x => x.Checksum)
            .FirstOrDefaultAsync(cancellationToken);

        return (analysisDate, string.IsNullOrWhiteSpace(datasetVersion) ? "unknown" : datasetVersion);
    }

    /// <summary>
    /// Recorded recommendations, newest and highest-priority first. Optionally narrowed to a lifecycle
    /// status. Paged — an unbounded result set must never reach the browser.
    /// </summary>
    public async Task<(IReadOnlyList<Recommendation> Items, int TotalCount)> ListAsync(
        RecommendationStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Recommendations.AsNoTracking();

        if (status is { } wanted)
        {
            query = query.Where(r => r.CurrentStatus == wanted);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.CreatedAtUtc)
            .ThenBy(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <summary>
    /// The deterministic idempotency key. Deliberately includes the ruleset version and analysis date:
    /// the same subject re-assessed under changed rules or on a later date is a genuinely new
    /// recommendation and must get its own frozen record.
    /// </summary>
    internal static string IdempotencyKey(Decision candidate, DateOnly analysisDate) =>
        string.Join(
            '|',
            RulesetVersion,
            analysisDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            candidate.Id,
            candidate.Title);

    private async Task<IReadOnlyList<Decision>> CollectAsync(CancellationToken cancellationToken)
    {
        var collected = new List<Decision>();

        // Sequential: providers share this request's scoped DbContext, which is not thread-safe.
        foreach (var provider in providers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                collected.AddRange(await provider.GetDecisionsAsync(cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // A failing context must not abort the whole run: the recommendations that *were*
                // produced are still worth recording. The gap is logged, never silently swallowed.
                logger.LogError(
                    ex, "Provider for area {Area} failed during generation; its recommendations are not recorded.",
                    provider.Area);
            }
        }

        return collected;
    }

    /// <summary>
    /// Drops a failed insert and its status events from the change tracker, so a lost race does not
    /// leave the context holding entities that every later save would retry.
    /// </summary>
    private void Detach(Recommendation recommendation)
    {
        foreach (var statusEvent in recommendation.StatusEvents)
        {
            db.Entry(statusEvent).State = EntityState.Detached;
        }

        db.Entry(recommendation).State = EntityState.Detached;
    }
}
