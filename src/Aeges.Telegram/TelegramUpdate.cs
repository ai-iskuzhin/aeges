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
/// <param name="SenderUserId">The Telegram user identifier for the sender, when available.</param>
/// <param name="MessageId">The inbound message identifier, or callback message identifier.</param>
/// <param name="MessageThreadId">The Telegram forum topic/thread identifier, when available.</param>
/// <param name="ReplyToMessageId">The message identifier this message replies to, when available.</param>
/// <param name="IsPrivateChat">A value indicating whether the update came from a private chat.</param>
public sealed record TelegramUpdate(
    long ChatId,
    string? Text = null,
    string? CallbackData = null,
    string? Username = null,
    string? FirstName = null,
    string? LastName = null,
    long? SenderUserId = null,
    int? MessageId = null,
    int? MessageThreadId = null,
    int? ReplyToMessageId = null,
    bool IsPrivateChat = true)
{
    /// <summary>
    /// Converts the observed Telegram sender fields into a domain profile.
    /// </summary>
    /// <returns>The observed Telegram profile.</returns>
    public RuntimeTelegramUserProfile ToUserProfile() => new(Username, FirstName, LastName);
}
