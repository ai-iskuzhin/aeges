namespace Aeges.Telegram;

/// <summary>
/// Defines Telegram network operations used by the runtime transport loop.
/// </summary>
public interface ITelegramBotGateway
{
    /// <summary>
    /// Gets inbound Telegram updates.
    /// </summary>
    /// <param name="offset">The next update offset to request.</param>
    /// <param name="limit">The maximum number of updates to return.</param>
    /// <param name="timeoutSeconds">The long-polling timeout in seconds.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The inbound updates understood by Aeges.</returns>
    Task<IReadOnlyList<TelegramBotUpdate>> GetUpdatesAsync(
        int? offset,
        int limit,
        int timeoutSeconds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sends a response message with inline buttons.
    /// </summary>
    /// <param name="chatId">The target Telegram chat identifier.</param>
    /// <param name="response">The response produced by the interaction handler.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    Task SendResponseAsync(
        long chatId,
        TelegramResponse response,
        CancellationToken cancellationToken);

    /// <summary>
    /// Answers an inline keyboard callback query.
    /// </summary>
    /// <param name="callbackQueryId">The Telegram callback query identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    Task AnswerCallbackQueryAsync(
        string callbackQueryId,
        CancellationToken cancellationToken);
}
