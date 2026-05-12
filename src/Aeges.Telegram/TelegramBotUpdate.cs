namespace Aeges.Telegram;

/// <summary>
/// Represents a Telegram update normalized for the Aeges button interaction handler.
/// </summary>
/// <param name="UpdateId">The Telegram update identifier.</param>
/// <param name="ChatId">The source chat identifier.</param>
/// <param name="Text">The inbound message text.</param>
/// <param name="CallbackData">The inline button callback payload.</param>
/// <param name="CallbackQueryId">The callback query identifier to acknowledge.</param>
/// <param name="MessageId">The Telegram message identifier that can be edited for callback navigation.</param>
/// <param name="Username">The Telegram username without an at-sign, when available.</param>
/// <param name="FirstName">The Telegram first name, when available.</param>
/// <param name="LastName">The Telegram last name, when available.</param>
public sealed record TelegramBotUpdate(
    int UpdateId,
    long ChatId,
    string? Text,
    string? CallbackData,
    string? CallbackQueryId,
    int? MessageId = null,
    string? Username = null,
    string? FirstName = null,
    string? LastName = null);
