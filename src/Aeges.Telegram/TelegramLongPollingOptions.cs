namespace Aeges.Telegram;

/// <summary>
/// Configures the Telegram long-polling loop.
/// </summary>
/// <param name="Limit">The maximum number of updates to fetch in one poll.</param>
/// <param name="TimeoutSeconds">The Telegram long-polling timeout in seconds.</param>
public sealed record TelegramLongPollingOptions(int Limit = 50, int TimeoutSeconds = 30);
