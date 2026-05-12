using Aeges.Core;

namespace Aeges.Telegram;

/// <summary>
/// Describes a Telegram inbound message or button callback.
/// </summary>
/// <param name="ChatId">The Telegram chat identifier.</param>
/// <param name="Text">The inbound text, when the update is a message.</param>
/// <param name="CallbackData">The button callback payload, when the update is a callback query.</param>
/// <param name="Username">The Telegram username without an at-sign, when available.</param>
/// <param name="FirstName">The Telegram first name, when available.</param>
/// <param name="LastName">The Telegram last name, when available.</param>
public sealed record TelegramUpdate(
    long ChatId,
    string? Text = null,
    string? CallbackData = null,
    string? Username = null,
    string? FirstName = null,
    string? LastName = null)
{
    /// <summary>
    /// Converts the observed Telegram sender fields into a domain profile.
    /// </summary>
    /// <returns>The observed Telegram profile.</returns>
    public RuntimeTelegramUserProfile ToUserProfile() => new(Username, FirstName, LastName);
}
