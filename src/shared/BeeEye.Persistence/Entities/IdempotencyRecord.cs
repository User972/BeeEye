namespace BeeEye.Persistence.Entities;

/// <summary>
/// One honoured <c>Idempotency-Key</c> and the response it produced (ADR 0007 §2.1).
/// <para>
/// The row is written <b>in the same transaction as the effect</b>, so a key and the change it
/// authorised commit or roll back together. A replay of the same key returns the stored status and
/// body verbatim without re-running the handler; the same key with a different body is a client
/// error, not a second effect.
/// </para>
/// <para>
/// <c>UNIQUE(Key)</c> is the concurrency guard: two simultaneous submissions of the same key race on
/// the index, and the loser is refused rather than applied twice.
/// </para>
/// </summary>
public class IdempotencyRecord
{
    public Guid Id { get; set; }

    /// <summary>The client-supplied key. Unique across the platform.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The route the key was used on, stored so a replay can be checked against it.</summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>
    /// Stable hash over route, canonicalised request payload and principal. Property-order changes in
    /// the JSON do not change it; a genuinely different request does.
    /// </summary>
    public string RequestFingerprint { get; set; } = string.Empty;

    public int ResponseStatus { get; set; }

    /// <summary>The response body, returned verbatim on replay.</summary>
    public string ResponseBody { get; set; } = string.Empty;

    /// <summary>Stable subject id of the caller. A key is scoped to the principal that first used it.</summary>
    public string PrincipalId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// When the key stops being remembered. Past this point it is treated as unseen, so the retention
    /// window must exceed any client's retry horizon (ADR 0007 §2.1 sets the default at 48 hours).
    /// </summary>
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
