namespace Aeges.Application;

/// <summary>
/// Provides deterministic time to application use cases.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the current timestamp.
    /// </summary>
    DateTimeOffset Now { get; }
}
