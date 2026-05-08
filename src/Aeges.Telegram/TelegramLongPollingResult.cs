namespace Aeges.Telegram;

/// <summary>
/// Describes the result of one Telegram polling batch.
/// </summary>
/// <param name="NextOffset">The next update offset that should be requested.</param>
/// <param name="ProcessedUpdates">The number of updates processed by the runtime.</param>
public sealed record TelegramLongPollingResult(int? NextOffset, int ProcessedUpdates);
