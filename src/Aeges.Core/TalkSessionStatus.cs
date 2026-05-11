namespace Aeges.Core;

/// <summary>
/// Defines the lifecycle state of a durable discussion session.
/// </summary>
public enum TalkSessionStatus
{
    /// <summary>
    /// The discussion can accept new messages.
    /// </summary>
    Open = 0,

    /// <summary>
    /// The discussion is retained for audit but no longer accepts new messages.
    /// </summary>
    Archived = 1,
}

/// <summary>
/// Provides conversion helpers for <see cref="TalkSessionStatus"/>.
/// </summary>
public static class TalkSessionStatusExtensions
{
    /// <summary>
    /// Converts a talk session status into its stable storage representation.
    /// </summary>
    /// <param name="status">The status to convert.</param>
    /// <returns>The lowercase storage value.</returns>
    public static string ToStorageValue(this TalkSessionStatus status) => status switch
    {
        TalkSessionStatus.Open => "open",
        TalkSessionStatus.Archived => "archived",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown talk session status."),
    };

    /// <summary>
    /// Parses a stable storage value into a talk session status.
    /// </summary>
    /// <param name="value">The persisted status value.</param>
    /// <returns>The matching status.</returns>
    public static TalkSessionStatus FromStorageValue(string value) => value switch
    {
        "open" => TalkSessionStatus.Open,
        "archived" => TalkSessionStatus.Archived,
        _ => throw new ArgumentException($"Unknown talk session status '{value}'.", nameof(value)),
    };
}
