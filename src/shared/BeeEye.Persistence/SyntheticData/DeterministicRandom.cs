using System.Security.Cryptography;
using System.Text;

namespace BeeEye.Persistence.SyntheticData;

/// <summary>
/// A tiny seeded PRNG (SplitMix64) for the synthetic after-sales/parts generator. Every "random-looking"
/// value is derived from a hash of a stable key + the global seed, so the whole dataset is reproducible:
/// same inputs ⇒ identical output ⇒ a re-run is skipped. There is <b>no</b> unseeded randomness or wall
/// clock anywhere in generation.
/// </summary>
public sealed class DeterministicRandom
{
    private ulong _state;

    public DeterministicRandom(ulong seed) => _state = seed;

    /// <summary>Seeds a stream from a global seed mixed with a stable string key.</summary>
    public static DeterministicRandom FromKey(ulong globalSeed, string key) => new(Hash(globalSeed, key));

    /// <summary>Next 64-bit value (SplitMix64).</summary>
    public ulong NextUlong()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Uniform double in [0, 1).</summary>
    public double NextDouble() => (NextUlong() >> 11) * (1.0 / (1UL << 53));

    /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            return minInclusive;
        }

        return minInclusive + (int)(NextUlong() % (ulong)(maxExclusive - minInclusive));
    }

    /// <summary>FNV-1a 64-bit hash of <paramref name="key"/> mixed with <paramref name="seed"/> (never 0).</summary>
    public static ulong Hash(ulong seed, string key)
    {
        var h = 1469598103934665603UL ^ seed;
        foreach (var c in key)
        {
            h ^= c;
            h *= 1099511628211UL;
        }

        return h == 0 ? 0x9E3779B97F4A7C15UL : h;
    }

    /// <summary>A deterministic GUID from a stable key (first 16 bytes of its SHA-256).</summary>
    public static Guid DeterministicGuid(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash.AsSpan(0, 16));
    }

    private const string Base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>Encodes a value as fixed-width base-36 (left-padded/wrapped), for synthetic VINs.</summary>
    public static string ToBase36(ulong value, int width)
    {
        Span<char> buffer = stackalloc char[width];
        for (var i = width - 1; i >= 0; i--)
        {
            buffer[i] = Base36[(int)(value % 36)];
            value /= 36;
        }

        return new string(buffer);
    }
}
