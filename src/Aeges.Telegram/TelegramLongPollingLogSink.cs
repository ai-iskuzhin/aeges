namespace Aeges.Telegram;

/// <summary>
/// Receives sanitized Telegram long-polling diagnostic events.
/// </summary>
/// <param name="entry">The diagnostic event.</param>
/// <param name="cancellationToken">A token that cancels diagnostic delivery.</param>
/// <returns>A task that completes when the diagnostic event has been handled.</returns>
public delegate ValueTask TelegramLongPollingLogSink(
    TelegramLongPollingLogEntry entry,
    CancellationToken cancellationToken);
