namespace Aeges.Telegram;

/// <summary>
/// Describes one observable Telegram long-polling diagnostic event.
/// </summary>
/// <param name="Level">The diagnostic severity.</param>
/// <param name="Message">The sanitized diagnostic message.</param>
/// <param name="NextOffset">The next Telegram update offset, when known.</param>
/// <param name="ProcessedUpdates">The number of updates processed, when known.</param>
/// <param name="ExceptionType">The exception type for warning events, when available.</param>
/// <param name="ErrorMessage">The sanitized error message for warning events, when available.</param>
public sealed record TelegramLongPollingLogEntry(
    TelegramLongPollingLogLevel Level,
    string Message,
    int? NextOffset = null,
    int? ProcessedUpdates = null,
    string? ExceptionType = null,
    string? ErrorMessage = null);
