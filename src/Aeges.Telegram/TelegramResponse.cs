namespace Aeges.Telegram;

/// <summary>
/// Describes a Telegram response message and its inline buttons.
/// </summary>
/// <param name="Text">The response text.</param>
/// <param name="Buttons">The inline button markup.</param>
public sealed record TelegramResponse(string Text, TelegramButtonMarkup Buttons);
