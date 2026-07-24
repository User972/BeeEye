namespace BeeEye.Shared.Security;

/// <summary>
/// The data boundary a caller is authorised to see. Enforced server-side on every
/// query, export, count and dashboard so users cannot infer unauthorised data —
/// including through aggregate totals. An empty allow-list means "not restricted on
/// that dimension" only when <see cref="IsUnrestricted"/> is set by an administrator role.
/// </summary>
public sealed record DataScope
{
    public required Guid TenantId { get; init; }

    public IReadOnlyCollection<Guid> BusinessUnitIds { get; init; } = [];
    public IReadOnlyCollection<Guid> RegionIds { get; init; } = [];
    public IReadOnlyCollection<Guid> BranchIds { get; init; } = [];
    public IReadOnlyCollection<Guid> LocationIds { get; init; } = [];
    public IReadOnlyCollection<string> ProductCategories { get; init; } = [];

    /// <summary>True for platform/administrator roles with tenant-wide visibility.</summary>
    public bool IsUnrestricted { get; init; }
}
