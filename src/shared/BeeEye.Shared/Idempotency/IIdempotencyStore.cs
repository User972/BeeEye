namespace BeeEye.Shared.Idempotency;

/// <summary>A previously honoured key and the response it produced.</summary>
/// <param name="Key">The client-supplied key.</param>
/// <param name="Route">The route it was used on.</param>
/// <param name="RequestFingerprint">Hash of route + canonical payload + principal.</param>
/// <param name="ResponseStatus">The status the first attempt returned.</param>
/// <param name="ResponseBody">The body the first attempt returned, replayed verbatim.</param>
/// <param name="PrincipalId">Subject id of the caller that first used the key.</param>
public sealed record IdempotencyEntry(
    string Key,
    string Route,
    string RequestFingerprint,
    int ResponseStatus,
    string ResponseBody,
    string PrincipalId);

/// <summary>
/// Persistence seam for <c>Idempotency-Key</c> handling (ADR 0007 §2.1).
/// <para>
/// Declared in the dependency-free kernel so the endpoint filter in <c>BeeEye.Shared.Web</c> can
/// enforce the protocol without taking a dependency on EF Core, and the EF implementation can live in
/// <c>BeeEye.Persistence</c> beside the rest of the data access.
/// </para>
/// <para>
/// The transaction methods exist because the ADR's guarantee is not "a key is recorded" but "the key
/// and the effect it authorised commit or roll back <b>together</b>". A store that could only insert
/// rows would let a crash leave an effect with no key — and the next retry would apply it twice.
/// </para>
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// The entry for <paramref name="key"/>, or null if unseen. An entry past its retention window is
    /// treated as unseen and removed, so an expired key behaves exactly like a fresh one.
    /// </summary>
    Task<IdempotencyEntry?> FindAsync(string key, CancellationToken cancellationToken);

    /// <summary>Opens the transaction the effect and the key row will share.</summary>
    Task BeginAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Records <paramref name="entry"/> and commits, publishing the effect with it.
    /// </summary>
    /// <returns>
    /// False when another request committed the same key first — the caller must report a conflict
    /// rather than a second effect. The unique index, not this check, is the guarantee.
    /// </returns>
    Task<bool> TryCompleteAsync(IdempotencyEntry entry, CancellationToken cancellationToken);

    /// <summary>Abandons the transaction, discarding the effect along with the key.</summary>
    Task RollbackAsync(CancellationToken cancellationToken);
}
