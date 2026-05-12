namespace Aeges.Core;

/// <summary>
/// Stores the human-readable Telegram profile fields observed from inbound updates.
/// </summary>
/// <param name="Username">The Telegram username without an at-sign, when available.</param>
/// <param name="FirstName">The Telegram first name, when available.</param>
/// <param name="LastName">The Telegram last name, when available.</param>
public sealed record RuntimeTelegramUserProfile(string? Username, string? FirstName, string? LastName)
{
    /// <summary>
    /// Gets an empty Telegram profile.
    /// </summary>
    public static RuntimeTelegramUserProfile Empty { get; } = new(null, null, null);

    /// <summary>
    /// Gets a value indicating whether the profile contains no observed user-facing fields.
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Username)
        && string.IsNullOrWhiteSpace(FirstName)
        && string.IsNullOrWhiteSpace(LastName);

    /// <summary>
    /// Returns a profile with trimmed values and empty strings normalized to null.
    /// </summary>
    /// <returns>The normalized profile.</returns>
    public RuntimeTelegramUserProfile Normalize() =>
        new(NormalizeValue(Username), NormalizeValue(FirstName), NormalizeValue(LastName));

    private static string? NormalizeValue(string? value)
    {
        var normalized = value?.Trim();

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
