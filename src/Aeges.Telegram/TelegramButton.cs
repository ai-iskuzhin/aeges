namespace Aeges.Telegram;

/// <summary>
/// Represents an inline Telegram button with runtime-owned callback data.
/// </summary>
/// <param name="Text">The visible button text.</param>
/// <param name="CallbackData">The callback payload returned to Aeges when the button is pressed.</param>
/// <param name="Style">The optional Telegram button presentation style.</param>
public sealed record TelegramButton(
    string Text,
    string CallbackData,
    TelegramButtonStyle Style = TelegramButtonStyle.Default);
