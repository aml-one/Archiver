namespace Archiver.Core.Services;

/// <summary>
/// Abstraction over DateTime for testability.
/// </summary>
public interface ISystemClock
{
    DateTime UtcNow { get; }
    DateTime Now { get; }
}

public sealed class SystemClock : ISystemClock
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Now => DateTime.Now;
}
