namespace BeeEye.Analytics.Decisions;

/// <summary>
/// The published cross-context contract for the Executive Decision Cockpit (UC8).
/// <para>
/// Each bounded context contributes the material exceptions it alone knows how to detect. The
/// cockpit consumes <see cref="IDecisionSignalProvider"/> instances and never reaches into another
/// module: this is the "published contract" seam required by the module-boundary rules
/// (<c>docs/architecture/module-boundaries.md</c>), and it is what lets the cockpit aggregate across
/// seven use cases without any module referencing another.
/// </para>
/// <para>
/// The interface lives in <c>BeeEye.Analytics</c> because every live module already references it,
/// so the seam adds no new coupling. It stays framework-free — no EF, no ASP.NET — so providers
/// remain unit-testable.
/// </para>
/// </summary>
public interface IDecisionSignalProvider
{
    /// <summary>
    /// Business area this provider raises decisions for, e.g. "Inventory". Used for diagnostics and
    /// to attribute a failure to the context that caused it.
    /// </summary>
    string Area { get; }

    /// <summary>
    /// The decisions this context currently considers material. Returns an empty list — never
    /// throws — when the context has no data or nothing crosses its thresholds.
    /// </summary>
    /// <remarks>
    /// Implementations should return only their most significant candidates rather than every row
    /// they can score; the cockpit is a decision queue, not a report.
    /// </remarks>
    Task<IReadOnlyList<Decision>> GetDecisionsAsync(CancellationToken cancellationToken);
}
