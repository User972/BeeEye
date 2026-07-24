namespace BeeEye.Shared.Security;

/// <summary>
/// Comparison of the stable subject ids that identify a person across the platform.
/// <para>
/// This exists because "is the approver the same person who decided?" is a security question, not a
/// string question. Segregation of duties (ADR 0006 §6) depends on the answer, so the comparison lives
/// in one tested place rather than being re-improvised at each call site — where one <c>OrdinalIgnoreCase</c>
/// or one forgotten <c>Trim</c> would quietly open a self-approval path.
/// </para>
/// </summary>
public static class SubjectIds
{
    /// <summary>
    /// True when both values identify the same person.
    /// <para>
    /// Ordinal on trimmed values. Trimmed because whitespace can survive a claim round-trip and must
    /// not create two identities for one person; <b>ordinal</b> because subject ids are opaque
    /// identifiers where case is significant — treating <c>abc</c> and <c>ABC</c> as one person would
    /// be a different bug, and a worse one.
    /// </para>
    /// <para>
    /// A null or blank value matches nothing, including another blank: an unidentified actor is not
    /// "the same person" as another unidentified actor.
    /// </para>
    /// </summary>
    public static bool Same(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left)
        && !string.IsNullOrWhiteSpace(right)
        && string.Equals(left.Trim(), right.Trim(), StringComparison.Ordinal);
}
