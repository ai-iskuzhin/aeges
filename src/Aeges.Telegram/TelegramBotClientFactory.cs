using Aeges.Application.Configuration;
using Telegram.Bot;

namespace Aeges.Telegram;

/// <summary>
/// Creates Telegram Bot API clients from runtime configuration.
/// </summary>
public static class TelegramBotClientFactory
{
    /// <summary>
    /// Creates a Telegram bot client using the configured token environment variable.
    /// </summary>
    /// <param name="configuration">The Telegram configuration.</param>
    /// <param name="cancellationToken">The global client cancellation token.</param>
    /// <returns>A Telegram Bot API client.</returns>
    /// <exception cref="TelegramTransportException">Thrown when the token environment variable is not set.</exception>
    public static ITelegramBotClient Create(
        AegesTelegramConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var token = Environment.GetEnvironmentVariable(configuration.BotTokenEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new TelegramTransportException(
                $"Telegram bot token environment variable '{configuration.BotTokenEnvironmentVariable}' is not set.");
        }

        return new TelegramBotClient(token, cancellationToken: cancellationToken);
    }
}
