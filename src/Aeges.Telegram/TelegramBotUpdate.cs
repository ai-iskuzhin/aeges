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
public sealed record TelegramBotUpdate(
    int UpdateId,
    long ChatId,
    string? Text,
    string? CallbackData,
    string? CallbackQueryId,
    int? MessageId = null);
