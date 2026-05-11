namespace Aeges.Telegram;

using Aeges.Core;
using System.Collections.Concurrent;

/// <summary>
/// Runs a button-first Telegram long-polling loop around the interaction handler.
/// </summary>
public sealed class TelegramLongPollingService
{
    private readonly ITelegramBotGateway gateway;
    private readonly TelegramInteractionHandler handler;
    private readonly ConcurrentDictionary<TaskWatchKey, TaskWatch> taskWatches = new();

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
                var sentMessageId = await gateway.SendResponseAsync(update.ChatId, response, cancellationToken);
                await TrackResponseAsync(update.ChatId, sentMessageId, response, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(update.CallbackQueryId) && update.MessageId is not null)
            {
                await TrackResponseAsync(update.ChatId, update.MessageId, response, cancellationToken);
            }

            nextOffset = Math.Max(nextOffset ?? 0, update.UpdateId + 1);
            processed++;
        }

        await NotifyTaskWatchersAsync(cancellationToken);

        return new TelegramLongPollingResult(nextOffset, processed);
    }

    private async Task TrackResponseAsync(
        long chatId,
        int? messageId,
        TelegramResponse response,
        CancellationToken cancellationToken)
    {
        var metadata = response.Metadata;

        foreach (var pair in taskWatches.Where(pair => pair.Key.ChatId == chatId))
        {
            taskWatches[pair.Key] = pair.Value with
            {
                LastBotMessageIsTaskDetails = false,
            };
        }

        if (metadata?.TaskId is not { } taskId)
        {
            return;
        }

        var task = await handler.GetTaskOrDefaultAsync(taskId, cancellationToken);

        if (task is null)
        {
            return;
        }

        var isDetails = metadata.Kind == TelegramResponseKind.TaskDetails;
        var key = new TaskWatchKey(chatId, taskId);
        taskWatches[key] = new TaskWatch(
            chatId,
            taskId,
            CreateTaskFingerprint(task),
            isDetails ? messageId : null,
            isDetails && messageId is not null);
    }

    private async Task NotifyTaskWatchersAsync(CancellationToken cancellationToken)
    {
        foreach (var pair in taskWatches.ToArray())
        {
            var task = await handler.GetTaskOrDefaultAsync(pair.Key.TaskId, cancellationToken);

            if (task is null)
            {
                taskWatches.TryRemove(pair.Key, out _);
                continue;
            }

            var fingerprint = CreateTaskFingerprint(task);

            if (fingerprint == pair.Value.LastFingerprint)
            {
                continue;
            }

            if (pair.Value.LastBotMessageIsTaskDetails && pair.Value.DetailMessageId is not null)
            {
                var response = await handler.TokenizeResponseAsync(
                    pair.Key.ChatId,
                    await handler.RenderTaskDetailsAsync(task.Id, cancellationToken),
                    cancellationToken);
                await gateway.EditResponseAsync(pair.Key.ChatId, pair.Value.DetailMessageId.Value, response, cancellationToken);
                taskWatches[pair.Key] = pair.Value with
                {
                    LastFingerprint = fingerprint,
                    LastBotMessageIsTaskDetails = true,
                };
                continue;
            }

            var notification = await handler.TokenizeResponseAsync(
                pair.Key.ChatId,
                new TelegramResponse(
                    $"""
                    Task updated: {task.Id}
                    Status: {task.Status.ToStorageValue()}
                    Iterations: {task.CurrentIteration}/{task.MaxIterations}
                    Failure:
                    {TelegramMarkdown.Quote(task.FailureReason ?? "(none)")}
                    """,
                    new TelegramButtonMarkup(
                    [
                        [new TelegramButton("View task", TelegramCallbackData.ViewTask(task.Id))],
                    ]),
                    new TelegramResponseMetadata(TelegramResponseKind.TaskWatch, task.Id)),
                cancellationToken);
            await gateway.SendResponseAsync(pair.Key.ChatId, notification, cancellationToken);

            taskWatches[pair.Key] = pair.Value with
            {
                LastFingerprint = fingerprint,
                DetailMessageId = null,
                LastBotMessageIsTaskDetails = false,
            };
        }
    }

    private static string CreateTaskFingerprint(RuntimeTask task) =>
        $"{task.Status.ToStorageValue()}:{task.CurrentIteration}:{task.FailureReason}";

    private readonly record struct TaskWatchKey(long ChatId, TaskId TaskId);

    private sealed record TaskWatch(
        long ChatId,
        TaskId TaskId,
        string LastFingerprint,
        int? DetailMessageId,
        bool LastBotMessageIsTaskDetails);
}
