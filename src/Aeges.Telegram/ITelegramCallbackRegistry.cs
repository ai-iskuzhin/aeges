namespace Aeges.Telegram;

/// <summary>
/// Stores and resolves short Telegram callback tokens.
/// </summary>
public interface ITelegramCallbackRegistry
{
    /// <summary>
    /// Rewrites response button callbacks into short transport-safe tokens.
    /// </summary>
    /// <param name="chatId">The Telegram chat identifier.</param>
    /// <param name="response">The response to tokenize.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The response with tokenized button callbacks.</returns>
    Task<TelegramResponse> TokenizeAsync(
        long chatId,
        TelegramResponse response,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves an inbound callback token into its original logical callback payload.
    /// </summary>
    /// <param name="chatId">The Telegram chat identifier.</param>
    /// <param name="callbackData">The inbound callback data.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The logical callback payload, or <see langword="null"/> when the token is invalid.</returns>
    Task<string?> ResolveAsync(
        long chatId,
        string callbackData,
        CancellationToken cancellationToken);
}
