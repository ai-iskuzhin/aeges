namespace Aeges.Application;

/// <summary>
/// Provides wall-clock UTC time.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}
