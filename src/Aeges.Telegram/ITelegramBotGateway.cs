namespace Aeges.Telegram;

/// <summary>
/// Defines Telegram network operations used by the runtime transport loop.
/// </summary>
public interface ITelegramBotGateway
{
    /// <summary>
    /// Gets the configured Telegram bot identity.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The configured bot identity.</returns>
    Task<TelegramBotIdentity> GetIdentityAsync(CancellationToken cancellationToken);

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
    /// <param name="messageThreadId">The target forum topic/thread identifier, when available.</param>
    /// <param name="response">The response produced by the interaction handler.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The sent message identifier, when the gateway provides it.</returns>
    Task<int?> SendResponseAsync(
        long chatId,
        int? messageThreadId,
        TelegramResponse response,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a forum topic in a Telegram supergroup.
    /// </summary>
    /// <param name="chatId">The target Telegram supergroup identifier.</param>
    /// <param name="name">The topic name.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The created forum topic.</returns>
    Task<TelegramForumTopic> CreateForumTopicAsync(
        long chatId,
        string name,
        CancellationToken cancellationToken);

    /// <summary>
    /// Edits an existing response message with updated text and inline buttons.
    /// </summary>
    /// <param name="chatId">The target Telegram chat identifier.</param>
    /// <param name="messageId">The message identifier to edit.</param>
    /// <param name="response">The response produced by the interaction handler.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    Task EditResponseAsync(
        long chatId,
        int messageId,
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
