namespace BeeEye.Analytics.Explainability;

/// <summary>
/// The published cross-context contract for the global explainability drawer (V3-DS-006).
/// <para>
/// Each bounded context explains its own output. The <c>Predictions</c> module consumes
/// <see cref="IExplainabilityProvider"/> instances and never reaches into another module: this is the
/// same "published contract" seam <see cref="Decisions.IDecisionSignalProvider"/> established for the
/// cockpit (<c>docs/architecture/module-boundaries.md</c>), and it is what lets one endpoint explain
/// eight use cases without any module referencing another.
/// </para>
/// <para>
/// The interface lives in <c>BeeEye.Analytics</c> because every live module already references it, so
/// the seam adds no new coupling. It stays framework-free — no EF, no ASP.NET — so providers remain
/// unit-testable.
/// </para>
/// <para>
/// <b>Nothing an implementation returns may be authored by a model.</b> An explanation restates what
/// the deterministic engine already computed; it never computes, alters or re-words a number, an
/// action or a verdict (ADR 0006 §2.6, <c>docs/architecture/overview.md</c> §8).
/// </para>
/// </summary>
public interface IExplainabilityProvider
{
    /// <summary>
    /// The subject kinds this provider explains, e.g. <c>"inventory-unit"</c>, <c>"forecast-scope"</c>.
    /// <para>
    /// A kind is claimed by <b>exactly one</b> provider. Two providers claiming the same kind is a
    /// wiring bug, and it fails at start-up rather than at request time — the request path is the
    /// wrong place to discover that the composition root is wrong.
    /// </para>
    /// </summary>
    IReadOnlySet<string> SubjectKinds { get; }

    /// <summary>
    /// Explains one subject, or returns <see langword="null"/> when it has nothing to explain.
    /// </summary>
    /// <param name="subjectKind">One of <see cref="SubjectKinds"/>.</param>
    /// <param name="subjectRef">The subject's identifier, in whatever form the context uses.</param>
    /// <param name="cancellationToken">Cancellation, propagated rather than swallowed.</param>
    /// <returns>
    /// The explanation, or <see langword="null"/> for <i>not mine / not found</i> — which the endpoint
    /// turns into a 404.
    /// <para>
    /// <b>Null and failure are different answers and must stay different.</b> A provider that cannot
    /// answer because something broke <b>throws</b>; the aggregation service catches it, logs it in
    /// full and reports a <i>gap</i>. Returning null on failure would tell the user "no explanation
    /// exists for this figure" when the truth is "we could not reach the data" — a far worse lie than
    /// an error message.
    /// </para>
    /// </returns>
    Task<Explanation?> ExplainAsync(string subjectKind, string subjectRef, CancellationToken cancellationToken);
}
