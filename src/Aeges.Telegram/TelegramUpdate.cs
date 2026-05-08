namespace Aeges.Telegram;

/// <summary>
/// Describes a Telegram inbound message or button callback.
/// </summary>
/// <param name="ChatId">The Telegram chat identifier.</param>
/// <param name="Text">The inbound text, when the update is a message.</param>
/// <param name="CallbackData">The button callback payload, when the update is a callback query.</param>
public sealed record TelegramUpdate(long ChatId, string? Text = null, string? CallbackData = null);
