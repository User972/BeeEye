namespace BeeEye.Shared.Web.Idempotency;

/// <summary>
/// Marks a request body that participates in the <c>Idempotency-Key</c> fingerprint.
/// <para>
/// Minimal-API argument binding runs <b>before</b> endpoint filters, so by the time the filter sees a
/// request the body stream has already been consumed. Rather than buffer and re-read every request —
/// which would tax reads to serve writes — the payload is identified explicitly: the filter serialises
/// the bound arguments that carry this marker and hashes those.
/// </para>
/// <para>
/// A consequence worth stating: re-serialising the bound object makes property order irrelevant by
/// construction, which is exactly the property ADR 0007 §2.1 requires of the fingerprint. An endpoint
/// with no body implements nothing and is fingerprinted by route and principal alone, which is the
/// whole of its request.
/// </para>
/// </summary>
public interface IIdempotentPayload;
