namespace BeeEye.Shared.Idempotency;

/// <summary>Why an <c>Idempotency-Key</c> header was refused.</summary>
public enum IdempotencyKeyProblem
{
    None,

    /// <summary>The header was absent or blank.</summary>
    Missing,

    /// <summary>The header was present but is not a usable key.</summary>
    Malformed,
}

/// <summary>
/// Validation of the client-supplied <c>Idempotency-Key</c> header (ADR 0007 §2.1).
/// <para>
/// Pure and framework-free so the rule is testable without a host, and shared so every mutating
/// endpoint enforces the same shape rather than each inventing its own.
/// </para>
/// </summary>
public static class IdempotencyKey
{
    /// <summary>The header clients send. Named once, so nothing spells it differently.</summary>
    public const string HeaderName = "Idempotency-Key";

    /// <summary>
    /// Longest accepted key. ADR 0007 asks for a UUIDv4 or ULID; the cap is generous enough for both
    /// plus a client prefix, and bounded so an unbounded header cannot become an unbounded index key.
    /// </summary>
    public const int MaxLength = 128;

    /// <summary>Shortest accepted key. A two-character "key" collides by accident, which defeats the point.</summary>
    public const int MinLength = 8;

    /// <summary>
    /// Validates the raw header value and normalises it (trimmed).
    /// </summary>
    /// <param name="raw">The header value as received; null or blank means the header was absent.</param>
    /// <param name="key">The normalised key when valid.</param>
    public static IdempotencyKeyProblem Validate(string? raw, out string key)
    {
        key = string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return IdempotencyKeyProblem.Missing;
        }

        var trimmed = raw.Trim();

        if (trimmed.Length is < MinLength or > MaxLength)
        {
            return IdempotencyKeyProblem.Malformed;
        }

        // Restricted to characters a UUID or ULID uses, plus separators a client prefix might carry.
        // Anything else is a client bug or an injection attempt; either way it is not a key we minted
        // an expectation about, so it is refused rather than stored.
        foreach (var c in trimmed)
        {
            var allowed = char.IsAsciiLetterOrDigit(c) || c is '-' || c is '_' || c is ':';
            if (!allowed)
            {
                return IdempotencyKeyProblem.Malformed;
            }
        }

        key = trimmed;
        return IdempotencyKeyProblem.None;
    }

    /// <summary>A safe, non-technical explanation naming the header, suitable for a 400 response.</summary>
    public static string Explain(IdempotencyKeyProblem problem) => problem switch
    {
        IdempotencyKeyProblem.None => "Accepted.",
        IdempotencyKeyProblem.Missing =>
            $"An '{HeaderName}' request header is required for this operation. Send a UUID that stays "
            + "the same across retries of the same intent, so a repeated submission cannot record a "
            + "second decision.",
        IdempotencyKeyProblem.Malformed =>
            $"The '{HeaderName}' header is not a usable key. Send a UUID or ULID between "
            + $"{MinLength} and {MaxLength} characters using letters, digits, '-', '_' or ':'.",
        _ => $"The '{HeaderName}' header was refused.",
    };
}
