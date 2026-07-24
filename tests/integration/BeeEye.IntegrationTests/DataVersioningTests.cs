using BeeEye.Persistence;
using BeeEye.Persistence.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeeEye.IntegrationTests;

/// <summary>
/// V3-PERF-001 — the shared <see cref="DataVersionResolver"/> that anchors the UC6/UC7 result cache.
/// Its correctness (latest sales month + newest ingestion checksum) touches the database, so — per the
/// repo convention that DB-reading code is proven end-to-end — it is asserted here against the seeded
/// store rather than with an in-memory substitute. Stability is what makes the cache key deterministic.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class DataVersioningTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task Resolver_returns_the_latest_sales_month_and_the_newest_ingestion_checksum()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BeeEyeDbContext>();
        var resolver = scope.ServiceProvider.GetRequiredService<DataVersionResolver>();

        var version = await resolver.CurrentAsync(CancellationToken.None);

        // AnalysisDate = the last day of the latest month that has sales.
        var latestMonth = await db.SalesFacts.AsNoTracking().MaxAsync(f => f.SaleMonth);
        var expectedDate = new DateOnly(latestMonth.Year, latestMonth.Month, DateTime.DaysInMonth(latestMonth.Year, latestMonth.Month));
        Assert.Equal(expectedDate, version.AnalysisDate);

        // DatasetVersion = the checksum of the newest completed ingestion (by CompletedAtUtc ?? StartedAtUtc).
        var newestChecksum = await db.IngestionBatches.AsNoTracking()
            .OrderByDescending(x => x.CompletedAtUtc ?? x.StartedAtUtc)
            .Select(x => x.Checksum)
            .FirstAsync();
        Assert.Equal(newestChecksum, version.DatasetVersion);
        Assert.False(string.IsNullOrWhiteSpace(version.DatasetVersion));
    }

    [Fact]
    public async Task Resolver_is_stable_across_calls_on_an_unchanged_database()
    {
        using var scope = factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<DataVersionResolver>();

        var first = await resolver.CurrentAsync(CancellationToken.None);
        var second = await resolver.CurrentAsync(CancellationToken.None);

        // DataVersion is a record: value equality proves the anchor (and therefore the cache key) is
        // deterministic on an unchanged database — the property the cache-hit proofs rely on.
        Assert.Equal(first, second);
    }
}
