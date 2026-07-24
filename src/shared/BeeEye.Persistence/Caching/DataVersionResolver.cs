using Microsoft.EntityFrameworkCore;

namespace BeeEye.Persistence.Caching;

/// <summary>
/// A content-addressed stamp of the data an analysis is computed against: the last month with sales and
/// the newest completed ingestion. Anchoring a cache to the data — rather than the wall clock — is what
/// makes the cache both safe and self-invalidating: the same database always yields the same stamp, and
/// a fresh ingestion (new checksum) yields a different one, so a superseded result can never be served.
/// </summary>
public sealed record DataVersion(DateOnly AnalysisDate, string DatasetVersion);

/// <summary>
/// Resolves the current <see cref="DataVersion"/> from two cheap indexed reads. This is the same anchor
/// <c>RecommendationRecordService.ResolveContextAsync</c> derives for its idempotency key; it is
/// deliberately *not* refactored onto this resolver (the write path's key must stay byte-identical — see
/// the S8 progress note), so the two cache sites accept one duplicated pair of queries in exchange for
/// zero risk to the governed write path.
/// </summary>
public sealed class DataVersionResolver(BeeEyeDbContext db)
{
    /// <summary>
    /// The last day of the latest month with sales, paired with the newest ingestion checksum. On an
    /// empty database it returns <see cref="DateOnly.MinValue"/> and <c>"unknown"</c> — a stable,
    /// clock-free sentinel, which is immaterial in practice because the two callers only compute after a
    /// <c>HasDataAsync</c> guard has already passed.
    /// </summary>
    public async Task<DataVersion> CurrentAsync(CancellationToken cancellationToken)
    {
        var latestMonth = await db.SalesFacts
            .AsNoTracking()
            .OrderByDescending(f => f.SaleMonth)
            .Select(f => (DateOnly?)f.SaleMonth)
            .FirstOrDefaultAsync(cancellationToken);

        var analysisDate = latestMonth is { } month
            ? new DateOnly(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month))
            : DateOnly.MinValue;

        var datasetVersion = await db.IngestionBatches
            .AsNoTracking()
            .OrderByDescending(x => x.CompletedAtUtc ?? x.StartedAtUtc)
            .Select(x => x.Checksum)
            .FirstOrDefaultAsync(cancellationToken);

        return new DataVersion(analysisDate, string.IsNullOrWhiteSpace(datasetVersion) ? "unknown" : datasetVersion);
    }
}
