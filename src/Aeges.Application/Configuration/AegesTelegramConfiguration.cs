namespace Aeges.Application.Configuration;

/// <summary>
/// Represents Telegram transport configuration.
/// </summary>
public sealed class AegesTelegramConfiguration
{
    /// <summary>
    /// Gets or sets the environment variable that contains the Telegram bot token.
    /// </summary>
    public string BotTokenEnvironmentVariable { get; set; } = "AEGES_TELEGRAM_BOT_TOKEN";

    /// <summary>
    /// Gets or sets the allowed Telegram chat identifiers.
    /// </summary>
    public List<long> AllowedChatIds { get; set; } = [];
}
