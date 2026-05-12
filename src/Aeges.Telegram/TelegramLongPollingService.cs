namespace Aeges.Telegram;

using Aeges.Core;
using System.Collections.Concurrent;
using ApiRequestException = global::Telegram.Bot.Exceptions.ApiRequestException;
using RequestException = global::Telegram.Bot.Exceptions.RequestException;

/// <summary>
/// Runs a button-first Telegram long-polling loop around the interaction handler.
/// </summary>
public sealed class TelegramLongPollingService
{
    private readonly ITelegramBotGateway gateway;
    private readonly TelegramInteractionHandler handler;
    private readonly ConcurrentDictionary<TaskWatchKey, TaskWatch> taskWatches = new();
    private readonly TimeSpan transientErrorDelay;
    private readonly TelegramLongPollingLogSink? logSink;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramLongPollingService"/> class.
    /// </summary>
    /// <param name="gateway">The Telegram network gateway.</param>
    /// <param name="handler">The deterministic interaction handler.</param>
    /// <param name="transientErrorDelay">The delay before retrying after a transient Telegram transport error.</param>
    /// <param name="logSink">The optional sanitized diagnostic event sink.</param>
    public TelegramLongPollingService(
        ITelegramBotGateway gateway,
        TelegramInteractionHandler handler,
        TimeSpan? transientErrorDelay = null,
        TelegramLongPollingLogSink? logSink = null)
    {
        this.gateway = gateway;
        this.handler = handler;
        this.transientErrorDelay = transientErrorDelay ?? TimeSpan.FromSeconds(2);
        this.logSink = logSink;
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
            try
            {
                var result = await PollOnceAsync(nextOffset, options, cancellationToken);
                nextOffset = result.NextOffset;
                if (result.ProcessedUpdates > 0)
                {
                    await LogAsync(
                        new TelegramLongPollingLogEntry(
                            TelegramLongPollingLogLevel.Information,
                            "Polling batch completed.",
                            result.NextOffset,
                            result.ProcessedUpdates),
                        cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (IsTransientTelegramTransportException(exception))
            {
                await LogAsync(
                    new TelegramLongPollingLogEntry(
                        TelegramLongPollingLogLevel.Warning,
                        "Transient Telegram polling failure; retrying.",
                        nextOffset,
                        ExceptionType: exception.GetType().Name,
                        ErrorMessage: exception.Message),
                    cancellationToken);

                if (transientErrorDelay > TimeSpan.Zero)
                {
                    await Task.Delay(transientErrorDelay, cancellationToken);
                }
            }
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
            try
            {
                var callbackQueryId = update.CallbackQueryId;
                var isCallback = !string.IsNullOrWhiteSpace(callbackQueryId);

                if (isCallback)
                {
                    await gateway.AnswerCallbackQueryAsync(callbackQueryId!, cancellationToken);
                }

                if (!isCallback && update.Text is not null && !TelegramInteractionHandler.IsStartCommand(update.Text))
                {
                    var pendingResponse = await handler.TokenizeResponseAsync(
                        update.ChatId,
                        TelegramInteractionHandler.RenderPendingTextResponse(update.Text),
                        cancellationToken);
                    var pendingMessageId = await gateway.SendResponseAsync(
                        update.ChatId,
                        update.MessageThreadId,
                        pendingResponse,
                        cancellationToken);
                    var finalResponse = await handler.HandleAsync(
                        ToInteractionUpdate(update),
                        cancellationToken);

                    if (pendingMessageId is not null)
                    {
                        await gateway.EditResponseAsync(
                            update.ChatId,
                            pendingMessageId.Value,
                            finalResponse,
                            cancellationToken);
                        await TrackResponseAsync(
                            update.ChatId,
                            update.MessageThreadId,
                            pendingMessageId,
                            finalResponse,
                            cancellationToken);
                    }
                    else
                    {
                        var finalMessageId = await gateway.SendResponseAsync(
                            update.ChatId,
                            update.MessageThreadId,
                            finalResponse,
                            cancellationToken);
                        await TrackResponseAsync(
                            update.ChatId,
                            update.MessageThreadId,
                            finalMessageId,
                            finalResponse,
                            cancellationToken);
                    }

                    nextOffset = Math.Max(nextOffset ?? 0, update.UpdateId + 1);
                    processed++;
                    continue;
                }

                var response = await handler.HandleAsync(
                    ToInteractionUpdate(update),
                    cancellationToken);

                if (isCallback && update.MessageId is not null)
                {
                    await gateway.EditResponseAsync(
                        update.ChatId,
                        update.MessageId.Value,
                        response,
                        cancellationToken);
                }
                else
                {
                    var sentMessageId = await gateway.SendResponseAsync(
                        update.ChatId,
                        update.MessageThreadId,
                        response,
                        cancellationToken);
                    await TrackResponseAsync(
                        update.ChatId,
                        update.MessageThreadId,
                        sentMessageId,
                        response,
                        cancellationToken);
                }

                if (isCallback && update.MessageId is not null)
                {
                    await TrackResponseAsync(
                        update.ChatId,
                        update.MessageThreadId,
                        update.MessageId,
                        response,
                        cancellationToken);
                }
            }
            catch (Exception exception) when (IsTelegramChatMigratedException(exception))
            {
                await LogAsync(
                    new TelegramLongPollingLogEntry(
                        TelegramLongPollingLogLevel.Warning,
                        "Telegram chat migrated to a supergroup; skipping update for the old chat id.",
                        NextOffset: update.UpdateId + 1,
                        ProcessedUpdates: processed + 1,
                        ExceptionType: exception.GetType().Name,
                        ErrorMessage: exception.Message),
                    cancellationToken);
            }
            catch (Exception exception) when (IsTelegramStaleCallbackException(exception))
            {
                await LogAsync(
                    new TelegramLongPollingLogEntry(
                        TelegramLongPollingLogLevel.Warning,
                        "Telegram callback query is stale; skipping update.",
                        NextOffset: update.UpdateId + 1,
                        ProcessedUpdates: processed + 1,
                        ExceptionType: exception.GetType().Name,
                        ErrorMessage: exception.Message),
                    cancellationToken);
            }

            nextOffset = Math.Max(nextOffset ?? 0, update.UpdateId + 1);
            processed++;
        }

        await NotifyTaskWatchersAsync(cancellationToken);

        return new TelegramLongPollingResult(nextOffset, processed);
    }

    private async Task TrackResponseAsync(
        long chatId,
        int? messageThreadId,
        int? messageId,
        TelegramResponse response,
        CancellationToken cancellationToken)
    {
        var metadata = response.Metadata;

        foreach (var pair in taskWatches.Where(pair => pair.Key.ChatId == chatId && pair.Key.MessageThreadId == messageThreadId))
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
        var key = new TaskWatchKey(chatId, messageThreadId, taskId);
        taskWatches[key] = new TaskWatch(
            chatId,
            messageThreadId,
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
                try
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
                }
                catch (Exception exception) when (IsTelegramChatMigratedException(exception))
                {
                    await ForgetMigratedTaskWatchAsync(pair.Key, exception, cancellationToken);
                }

                continue;
            }

            try
            {
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
                await gateway.SendResponseAsync(
                    pair.Key.ChatId,
                    pair.Key.MessageThreadId,
                    notification,
                    cancellationToken);

                taskWatches[pair.Key] = pair.Value with
                {
                    LastFingerprint = fingerprint,
                    DetailMessageId = null,
                    LastBotMessageIsTaskDetails = false,
                };
            }
            catch (Exception exception) when (IsTelegramChatMigratedException(exception))
            {
                await ForgetMigratedTaskWatchAsync(pair.Key, exception, cancellationToken);
            }
        }
    }

    private async ValueTask ForgetMigratedTaskWatchAsync(
        TaskWatchKey key,
        Exception exception,
        CancellationToken cancellationToken)
    {
        taskWatches.TryRemove(key, out _);
        await LogAsync(
            new TelegramLongPollingLogEntry(
                TelegramLongPollingLogLevel.Warning,
                "Telegram chat migrated to a supergroup; removing task watch for the old chat id.",
                ExceptionType: exception.GetType().Name,
                ErrorMessage: exception.Message),
            cancellationToken);
    }

    private static TelegramUpdate ToInteractionUpdate(TelegramBotUpdate update) =>
        new(
            update.ChatId,
            update.Text,
            update.CallbackData,
            update.Username,
            update.FirstName,
            update.LastName,
            update.SenderUserId,
            update.MessageId,
            update.MessageThreadId,
            update.ReplyToMessageId,
            update.IsPrivateChat);

    private static string CreateTaskFingerprint(RuntimeTask task) =>
        $"{task.Status.ToStorageValue()}:{task.CurrentIteration}:{task.FailureReason}";

    private static bool IsTransientTelegramTransportException(Exception exception) =>
        !IsTelegramChatMigratedException(exception)
        && !IsTelegramStaleCallbackException(exception)
        && exception is RequestException or HttpRequestException or IOException;

    private static bool IsTelegramChatMigratedException(Exception exception) =>
        exception is ApiRequestException apiException
        && (apiException.Parameters?.MigrateToChatId is not null
            || apiException.Message.Contains("group chat was upgraded to a supergroup chat", StringComparison.OrdinalIgnoreCase));

    private static bool IsTelegramStaleCallbackException(Exception exception) =>
        exception is ApiRequestException apiException
        && apiException.Message.Contains("query is too old", StringComparison.OrdinalIgnoreCase);

    private async ValueTask LogAsync(
        TelegramLongPollingLogEntry entry,
        CancellationToken cancellationToken)
    {
        if (logSink is not null)
        {
            await logSink(entry, cancellationToken);
        }
    }

    private readonly record struct TaskWatchKey(long ChatId, int? MessageThreadId, TaskId TaskId);

    private sealed record TaskWatch(
        long ChatId,
        int? MessageThreadId,
        TaskId TaskId,
        string LastFingerprint,
        int? DetailMessageId,
        bool LastBotMessageIsTaskDetails);
}
