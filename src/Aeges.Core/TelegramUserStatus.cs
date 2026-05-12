namespace Aeges.Core;

/// <summary>
/// Describes whether a Telegram user can operate the runtime.
/// </summary>
public enum TelegramUserStatus
{
    /// <summary>
    /// The user is waiting for an administrator decision.
    /// </summary>
    Pending,

    /// <summary>
    /// The user may use the runtime according to their role and access grants.
    /// </summary>
    Approved,

    /// <summary>
    /// The user was explicitly denied access.
    /// </summary>
    Denied,
}

/// <summary>
/// Provides stable storage values for <see cref="TelegramUserStatus"/>.
/// </summary>
public static class TelegramUserStatusExtensions
{
    /// <summary>
    /// Converts a status to its stable storage value.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>The stable storage value.</returns>
    public static string ToStorageValue(this TelegramUserStatus status) =>
        status switch
        {
            TelegramUserStatus.Pending => "pending",
            TelegramUserStatus.Approved => "approved",
            TelegramUserStatus.Denied => "denied",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown Telegram user status."),
        };

    /// <summary>
    /// Parses a status from its stable storage value.
    /// </summary>
    /// <param name="value">The storage value.</param>
    /// <returns>The parsed status.</returns>
    public static TelegramUserStatus FromStorageValue(string value) =>
        value switch
        {
            "pending" => TelegramUserStatus.Pending,
            "approved" => TelegramUserStatus.Approved,
            "denied" => TelegramUserStatus.Denied,
            _ => throw new ArgumentException($"Unknown Telegram user status '{value}'.", nameof(value)),
        };
}
