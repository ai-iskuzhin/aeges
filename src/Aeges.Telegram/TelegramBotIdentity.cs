namespace Aeges.Telegram;

/// <summary>
/// Describes the Telegram bot account used by the transport.
/// </summary>
/// <param name="Id">The Telegram user identifier for the bot.</param>
/// <param name="Username">The bot username.</param>
/// <param name="FirstName">The bot display first name.</param>
/// <param name="IsBot">A value indicating whether Telegram reports the identity as a bot.</param>
public sealed record TelegramBotIdentity(
    long Id,
    string? Username,
    string FirstName,
    bool IsBot);
