namespace Aeges.Telegram;

/// <summary>
/// Describes the outcome of a local agent restart requested from Telegram.
/// </summary>
/// <param name="Message">The human-readable restart summary.</param>
public sealed record TelegramAgentRestartResult(string Message);
