namespace Aeges.Telegram;

/// <summary>
/// Runs a button-first Telegram long-polling loop around the interaction handler.
/// </summary>
public sealed class TelegramLongPollingService
{
    private readonly ITelegramBotGateway gateway;
    private readonly TelegramInteractionHandler handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramLongPollingService"/> class.
    /// </summary>
    /// <param name="gateway">The Telegram network gateway.</param>
    /// <param name="handler">The deterministic interaction handler.</param>
    public TelegramLongPollingService(
        ITelegramBotGateway gateway,
        TelegramInteractionHandler handler)
    {
        this.gateway = gateway;
        this.handler = handler;
    }

    /// <summary>
    /// Runs Telegram long polling until cancellation is requested.
    /// </summary>
    /// <param name="options">The polling options.</param>
    /// <param name="cancellationToken">A token that stops the loop.</param>
    public async Task RunAsync(
        TelegramLongPollingOptions options,
        CancellationToken cancellationToken)
    {
        int? nextOffset = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await PollOnceAsync(nextOffset, options, cancellationToken);
            nextOffset = result.NextOffset;
        }
    }

    /// <summary>
    /// Processes one Telegram polling batch.
    /// </summary>
    /// <param name="nextOffset">The next update offset to request.</param>
    /// <param name="options">The polling options.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The processed batch result.</returns>
    public async Task<TelegramLongPollingResult> PollOnceAsync(
        int? nextOffset,
        TelegramLongPollingOptions options,
        CancellationToken cancellationToken)
    {
        var updates = await gateway.GetUpdatesAsync(
            nextOffset,
            options.Limit,
            options.TimeoutSeconds,
            cancellationToken);

        var processed = 0;

        foreach (var update in updates)
        {
            if (!string.IsNullOrWhiteSpace(update.CallbackQueryId))
            {
                await gateway.AnswerCallbackQueryAsync(update.CallbackQueryId, cancellationToken);
            }

            var response = await handler.HandleAsync(
                new TelegramUpdate(update.ChatId, update.Text, update.CallbackData),
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(update.CallbackQueryId) && update.MessageId is not null)
            {
                await gateway.EditResponseAsync(
                    update.ChatId,
                    update.MessageId.Value,
                    response,
                    cancellationToken);
            }
            else
            {
                await gateway.SendResponseAsync(update.ChatId, response, cancellationToken);
            }

            nextOffset = Math.Max(nextOffset ?? 0, update.UpdateId + 1);
            processed++;
        }

        return new TelegramLongPollingResult(nextOffset, processed);
    }
}
