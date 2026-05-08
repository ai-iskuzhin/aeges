using Aeges.Application.Configuration;
using Telegram.Bot;

namespace Aeges.Telegram;

/// <summary>
/// Creates Telegram Bot API clients from runtime configuration.
/// </summary>
public static class TelegramBotClientFactory
{
    /// <summary>
    /// Creates a Telegram bot client using the configured token source.
    /// </summary>
    /// <param name="configuration">The Telegram configuration.</param>
    /// <param name="cancellationToken">The global client cancellation token.</param>
    /// <returns>A Telegram Bot API client.</returns>
    /// <exception cref="TelegramTransportException">Thrown when no token source is configured.</exception>
    public static ITelegramBotClient Create(
        AegesTelegramConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var token = ResolveToken(configuration);

        return new TelegramBotClient(token, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates a Telegram gateway using the configured token source.
    /// </summary>
    /// <param name="configuration">The Telegram configuration.</param>
    /// <param name="cancellationToken">The global client cancellation token.</param>
    /// <returns>A Telegram network gateway.</returns>
    /// <exception cref="TelegramTransportException">Thrown when no token source is configured.</exception>
    public static ITelegramBotGateway CreateGateway(
        AegesTelegramConfiguration configuration,
        CancellationToken cancellationToken) =>
        new TelegramBotApiGateway(Create(configuration, cancellationToken));

    private static string ResolveToken(AegesTelegramConfiguration configuration)
    {
        var environmentToken = Environment.GetEnvironmentVariable(configuration.BotTokenEnvironmentVariable);

        if (!string.IsNullOrWhiteSpace(environmentToken))
        {
            return environmentToken;
        }

        if (!string.IsNullOrWhiteSpace(configuration.BotTokenFilePath)
            && File.Exists(configuration.BotTokenFilePath))
        {
            var fileToken = File.ReadAllText(configuration.BotTokenFilePath).Trim();

            if (!string.IsNullOrWhiteSpace(fileToken))
            {
                return fileToken;
            }
        }

        throw new TelegramTransportException(
            $"Telegram bot token is not configured. Set '{configuration.BotTokenEnvironmentVariable}', configure telegram.botTokenFilePath, or run 'aeges telegram setup'.");
    }
}
