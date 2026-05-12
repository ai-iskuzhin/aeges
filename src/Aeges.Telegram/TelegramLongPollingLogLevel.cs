namespace Aeges.Telegram;

/// <summary>
/// Describes the severity of a Telegram long-polling diagnostic event.
/// </summary>
public enum TelegramLongPollingLogLevel
{
    /// <summary>
    /// Informational polling progress.
    /// </summary>
    Information,

    /// <summary>
    /// A recoverable transport issue that the polling loop will retry.
    /// </summary>
    Warning,
}
