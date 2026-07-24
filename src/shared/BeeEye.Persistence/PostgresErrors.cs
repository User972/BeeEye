using Microsoft.EntityFrameworkCore;

namespace BeeEye.Persistence;

/// <summary>
/// Recognises the PostgreSQL error classes the write paths depend on.
/// <para>
/// Read from the provider exception's <c>SqlState</c> rather than by matching its message, which is
/// localised and version-dependent. Reflection keeps callers free of a direct Npgsql reference, so a
/// module can react to a constraint race without taking on the driver.
/// </para>
/// </summary>
public static class PostgresErrors
{
    /// <summary>SQLSTATE <c>23505</c> — a unique or primary-key constraint was violated.</summary>
    public const string UniqueViolation = "23505";

    /// <summary>
    /// True when <paramref name="exception"/> was caused by a unique-constraint violation.
    /// <para>
    /// Every write path in the platform leans on this: a unique index — not an application-level
    /// existence check — is what actually prevents a duplicate business record, so losing that race
    /// has to be distinguishable from a genuine failure.
    /// </para>
    /// </summary>
    public static bool IsUniqueViolation(DbUpdateException? exception) =>
        SqlStateOf(exception) == UniqueViolation;

    /// <summary>The provider's SQLSTATE for this failure, or null when it did not come from the server.</summary>
    public static string? SqlStateOf(DbUpdateException? exception)
    {
        var inner = exception?.InnerException;
        return inner is null
            ? null
            : inner.GetType().GetProperty("SqlState")?.GetValue(inner) as string;
    }
}
