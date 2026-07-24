using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace BeeEye.Persistence.Caching;

/// <summary>
/// A stampede-safe memoiser for the two recompute-heavy UC6/UC7 summary paths (V3-PERF-001). Callers hand
/// it a key that already encodes the <see cref="DataVersion"/> (and, for UC7, the scenario), so a hit is
/// only ever returned for byte-for-byte identical inputs — and the values it caches are pure,
/// deterministic, immutable analysis records. The cache therefore changes latency, never an answer: the
/// same inputs yield the same output whether freshly computed or served from the entry.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bounded by design.</b> The UC7 key embeds <c>serviceLevel</c>/<c>reviewPeriodMonths</c> — arbitrary
/// client query-string doubles — so the key space is client-controlled and effectively unbounded, not the
/// "handful of anchors" a data-version-only key would be. Two things keep memory bounded regardless:
/// a <b>private</b> <see cref="MemoryCache"/> with a hard <c>SizeLimit</c> (owning it lets us cap it
/// without forcing every other <see cref="IMemoryCache"/> consumer to declare an entry <c>Size</c>, which
/// a global limit would), plus a sliding window; and a per-key compute gate that is <b>pruned as soon as
/// its compute finishes</b>, so the gate registry only ever holds in-flight computes. Entry creation is
/// additionally throttled behind an expensive compute (a new key is always a miss → a full recompute), so
/// the key space cannot be flooded cheaply.
/// </para>
/// <para>
/// <b>Invalidation</b> is primarily by key change: a new ingestion mints a new checksum, hence a new key,
/// so a superseded result is simply never asked for again. The sliding window is the safety net.
/// </para>
/// <para>
/// <b>Stampede safety.</b> A per-key <see cref="SemaphoreSlim"/> serialises the first cold computation;
/// everyone who queued behind it observes the populated entry and computes nothing. A cancelled request
/// releases the gate without writing anything, so it neither poisons the entry nor blocks the next caller
/// — which recomputes under its own request scope. The compute runs under the caller's request scope and
/// token, so it may use the caller's request-scoped <c>DbContext</c> safely.
/// </para>
/// </remarks>
public sealed class DataVersionedCache : IDisposable
{
    /// <summary>Safety-net eviction: a superseded data version / stale scenario cannot linger past this.</summary>
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(15);

    // A private, bounded cache — NOT the shared app IMemoryCache — precisely so the client-controlled UC7
    // key space cannot grow it without bound. 512 entries is ample for the realistic scenario set (a few
    // data versions × the discrete UI scenarios) with headroom, and hard-caps a pathological flood.
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 512 });

    // Gates for in-flight computes only: each is removed the moment its compute completes (see the finally),
    // so this registry is bounded by concurrency, never by the key space.
    private readonly ConcurrentDictionary<object, SemaphoreSlim> _gates = new();
    private long _computeCount;

    /// <summary>
    /// The number of times the expensive factory has actually run. Exposed so a test can prove a cache
    /// hit deterministically — a second identical request must not increment it — rather than relying on
    /// a flaky wall-clock threshold.
    /// </summary>
    public long ComputeCount => Interlocked.Read(ref _computeCount);

    /// <summary>
    /// The number of in-flight compute gates. Exposed for tests: it returns to zero once no compute is
    /// running, proving the per-key gate registry is bounded (not leaked) regardless of the key space.
    /// </summary>
    public int InFlightGateCount => _gates.Count;

    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, computing it exactly once under concurrent
    /// cold misses.
    /// </summary>
    public async Task<T> GetOrComputeAsync<T>(
        object key, Func<CancellationToken, Task<T>> compute, CancellationToken cancellationToken)
        where T : class
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return cached;
        }

        var gate = _gates.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check: the request we queued behind may already have populated the entry.
            if (_cache.TryGetValue(key, out cached) && cached is not null)
            {
                return cached;
            }

            Interlocked.Increment(ref _computeCount);
            var result = await compute(cancellationToken).ConfigureAwait(false);
            _cache.Set(key, result, new MemoryCacheEntryOptions { Size = 1, SlidingExpiration = SlidingExpiration });
            return result;
        }
        finally
        {
            gate.Release();

            // Prune this gate now its compute is done. While the compute was in flight the gate stayed in
            // the registry so concurrent callers shared it (compute-once); the result is now cached, so any
            // later caller hits the cache rather than the gate. Identity-checked removal (key AND this exact
            // instance) so a slow releaser can never evict a newer gate a subsequent miss created for the
            // same key. This bounds the registry to in-flight computes regardless of the key space.
            _gates.TryRemove(new KeyValuePair<object, SemaphoreSlim>(key, gate));
        }
    }

    public void Dispose() => _cache.Dispose();
}
