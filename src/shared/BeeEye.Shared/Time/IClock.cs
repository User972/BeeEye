namespace BeeEye.Shared.Time;

/// <summary>
/// Abstraction over the current time so domain logic, aging calculations and tests
/// never read the wall clock directly. The platform stores UTC internally.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Production clock backed by the system UTC time.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
