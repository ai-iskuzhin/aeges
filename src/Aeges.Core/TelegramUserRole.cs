namespace Aeges.Core;

/// <summary>
/// Describes the local runtime role granted to a Telegram user.
/// </summary>
public enum TelegramUserRole
{
    /// <summary>
    /// The user can operate only explicitly granted projects and groups.
    /// </summary>
    User,

    /// <summary>
    /// The user can administer Telegram users and access grants.
    /// </summary>
    Admin,
}

/// <summary>
/// Provides stable storage values for <see cref="TelegramUserRole"/>.
/// </summary>
public static class TelegramUserRoleExtensions
{
    /// <summary>
    /// Converts a role to its stable storage value.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <returns>The stable storage value.</returns>
    public static string ToStorageValue(this TelegramUserRole role) =>
        role switch
        {
            TelegramUserRole.User => "user",
            TelegramUserRole.Admin => "admin",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown Telegram user role."),
        };

    /// <summary>
    /// Parses a role from its stable storage value.
    /// </summary>
    /// <param name="value">The storage value.</param>
    /// <returns>The parsed role.</returns>
    public static TelegramUserRole FromStorageValue(string value) =>
        value switch
        {
            "user" => TelegramUserRole.User,
            "admin" => TelegramUserRole.Admin,
            _ => throw new ArgumentException($"Unknown Telegram user role '{value}'.", nameof(value)),
        };
}
