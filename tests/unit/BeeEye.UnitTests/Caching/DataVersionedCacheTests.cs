using BeeEye.Persistence.Caching;
using Xunit;

namespace BeeEye.UnitTests.Caching;

/// <summary>
/// Tests for <see cref="DataVersionedCache"/> — the V3-PERF-001 stampede-safe memoiser. It holds no data
/// access (the caller supplies the compute delegate and the key) and owns its bounded cache, so its
/// behaviour is exercised directly with a counting delegate.
/// </summary>
public sealed class DataVersionedCacheTests
{
    private static DataVersionedCache NewCache() => new();

    /// <summary>An immutable result plus a call counter, standing in for a pure analysis record.</summary>
    private sealed record Payload(int Value);

    [Fact]
    public async Task A_cache_hit_returns_the_identical_value_without_recomputing()
    {
        var cache = NewCache();
        var calls = 0;
        Task<Payload> Compute(CancellationToken _) => Task.FromResult(new Payload(Interlocked.Increment(ref calls)));

        var first = await cache.GetOrComputeAsync("k", Compute, CancellationToken.None);
        var second = await cache.GetOrComputeAsync("k", Compute, CancellationToken.None);

        // Cached == fresh: the second call served the first computation verbatim and ran no compute.
        Assert.Same(first, second);
        Assert.Equal(1, calls);
        Assert.Equal(1, cache.ComputeCount);
    }

    [Fact]
    public async Task A_different_key_recomputes_and_is_isolated()
    {
        var cache = NewCache();
        var calls = 0;
        Task<Payload> Compute(CancellationToken _) => Task.FromResult(new Payload(Interlocked.Increment(ref calls)));

        var a = await cache.GetOrComputeAsync(("analysis", "v1"), Compute, CancellationToken.None);
        var b = await cache.GetOrComputeAsync(("analysis", "v2"), Compute, CancellationToken.None);

        // A new data version is a new key, so it recomputes rather than serving the stale entry —
        // this is how re-ingestion invalidates.
        Assert.NotSame(a, b);
        Assert.Equal(2, cache.ComputeCount);
    }

    [Fact]
    public async Task The_scenario_is_part_of_the_key_so_scenarios_never_cross_contaminate()
    {
        var cache = NewCache();
        var calls = 0;
        Task<Payload> Compute(CancellationToken _) => Task.FromResult(new Payload(Interlocked.Increment(ref calls)));

        // Same data version, two UC7 scenarios (serviceLevel differs) — distinct entries.
        var scenarioA = await cache.GetOrComputeAsync(("spare-parts:summary", "v1", "0.95", "1"), Compute, CancellationToken.None);
        var scenarioB = await cache.GetOrComputeAsync(("spare-parts:summary", "v1", "0.99", "1"), Compute, CancellationToken.None);
        var scenarioAAgain = await cache.GetOrComputeAsync(("spare-parts:summary", "v1", "0.95", "1"), Compute, CancellationToken.None);

        Assert.NotSame(scenarioA, scenarioB);
        Assert.Same(scenarioA, scenarioAAgain);
        Assert.Equal(2, cache.ComputeCount);
    }

    [Fact]
    public async Task Concurrent_cold_misses_compute_exactly_once()
    {
        var cache = NewCache();
        var calls = 0;
        var release = new TaskCompletionSource();

        async Task<Payload> SlowCompute(CancellationToken _)
        {
            var n = Interlocked.Increment(ref calls);
            await release.Task; // hold every entrant inside the compute until they are all queued
            return new Payload(n);
        }

        var racers = Enumerable.Range(0, 16)
            .Select(_ => cache.GetOrComputeAsync("k", SlowCompute, CancellationToken.None))
            .ToList();

        release.SetResult();
        var results = await Task.WhenAll(racers);

        // The per-key gate serialises the cold miss: one compute, and every racer observes the same result.
        Assert.Equal(1, calls);
        Assert.Equal(1, cache.ComputeCount);
        Assert.All(results, r => Assert.Same(results[0], r));
    }

    [Fact]
    public async Task A_cancelled_request_neither_poisons_the_entry_nor_blocks_the_next_caller()
    {
        var cache = NewCache();
        var calls = 0;

        using var cts = new CancellationTokenSource();
        async Task<Payload> Cancelling(CancellationToken ct)
        {
            Interlocked.Increment(ref calls);
            await cts.CancelAsync();
            ct.ThrowIfCancellationRequested();
            return new Payload(-1);
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cache.GetOrComputeAsync("k", Cancelling, cts.Token));

        // Nothing was cached (else this would throw or serve a poisoned entry), and the gate was released
        // (else this would deadlock) — a fresh request recomputes and succeeds with the new value.
        var recovered = await cache.GetOrComputeAsync("k", _ => Task.FromResult(new Payload(42)), CancellationToken.None);
        Assert.Equal(42, recovered.Value);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Gates_are_pruned_so_the_registry_never_grows_with_the_key_space()
    {
        var cache = NewCache();

        // Many distinct scenario keys (as the UC7 arbitrary-double key would produce), each computed once.
        for (var i = 0; i < 50; i++)
        {
            var n = i;
            await cache.GetOrComputeAsync($"scenario-{n}", _ => Task.FromResult(new Payload(n)), CancellationToken.None);
        }

        // No compute is in flight now, so every gate was pruned on completion — the registry is bounded by
        // concurrency, not by the (client-controlled, unbounded) key space.
        Assert.Equal(0, cache.InFlightGateCount);
    }

    [Fact]
    public async Task A_failed_compute_is_not_cached_and_the_next_caller_recovers()
    {
        var cache = NewCache();
        var attempts = 0;

        Task<Payload> Flaky(CancellationToken _)
        {
            var n = Interlocked.Increment(ref attempts);
            return n == 1 ? throw new InvalidOperationException("transient") : Task.FromResult(new Payload(n));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrComputeAsync("k", Flaky, CancellationToken.None));

        var recovered = await cache.GetOrComputeAsync("k", Flaky, CancellationToken.None);
        Assert.Equal(2, recovered.Value);
    }
}
