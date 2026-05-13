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
    private TelegramBotIdentity? botIdentity;

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
                var routingThreadId = GetRoutingThreadId(update);

                await LogAsync(
                    new TelegramLongPollingLogEntry(
                        TelegramLongPollingLogLevel.Information,
                        FormatUpdateForLog(update, routingThreadId)),
                    cancellationToken);

                if (isCallback)
                {
                    await TryAnswerCallbackQueryAsync(callbackQueryId!, update, processed, cancellationToken);
                }

                if (await TryStartTaskTopicAsync(update, cancellationToken))
                {
                    nextOffset = Math.Max(nextOffset ?? 0, update.UpdateId + 1);
                    processed++;
                    continue;
                }

                if (!isCallback && update.Text is not null && !TelegramInteractionHandler.IsStartCommand(update.Text))
                {
                    var pendingResponse = await handler.TokenizeResponseAsync(
                        update.ChatId,
                        TelegramInteractionHandler.RenderPendingTextResponse(update.Text),
                        cancellationToken);
                    var pendingMessageId = await gateway.SendResponseAsync(
                        update.ChatId,
                        routingThreadId,
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
                            routingThreadId,
                            pendingMessageId,
                            finalResponse,
                            cancellationToken);
                    }
                    else
                    {
                        var finalMessageId = await gateway.SendResponseAsync(
                            update.ChatId,
                            routingThreadId,
                            finalResponse,
                            cancellationToken);
                        await TrackResponseAsync(
                            update.ChatId,
                            routingThreadId,
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
                        routingThreadId,
                        response,
                        cancellationToken);
                    await TrackResponseAsync(
                        update.ChatId,
                        routingThreadId,
                        sentMessageId,
                        response,
                        cancellationToken);
                }

                if (isCallback && update.MessageId is not null)
                {
                    await TrackResponseAsync(
                        update.ChatId,
                        routingThreadId,
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

    private async Task<bool> TryStartTaskTopicAsync(
        TelegramBotUpdate update,
        CancellationToken cancellationToken)
    {
        if (update.CallbackData is not null || update.Text is null)
        {
            return false;
        }

        var routingThreadId = GetRoutingThreadId(update);

        if (update.IsPrivateChat && routingThreadId is null)
        {
            return false;
        }

        var identity = botIdentity ??= await gateway.GetIdentityAsync(cancellationToken);
        if (!TryParseNewTaskCommand(update.Text, identity.Username, out var topicTitle))
        {
            return false;
        }

        var messageThreadId = routingThreadId;

        if (!update.IsPrivateChat && messageThreadId is null)
        {
            try
            {
                var topic = await gateway.CreateForumTopicAsync(update.ChatId, topicTitle, cancellationToken);
                messageThreadId = topic.MessageThreadId;
            }
            catch (Exception exception) when (exception is RequestException or ApiRequestException)
            {
                var response = new TelegramResponse(
                    $"""
                    I could not create a task topic in this supergroup.

                    The bot probably needs administrator access with topic management enabled.

                    Error:
                    {TelegramMarkdown.Quote(exception.Message)}
                    """,
                    TelegramButtonMarkup.Empty);
                await gateway.SendResponseAsync(update.ChatId, update.MessageThreadId, response, cancellationToken);

                return true;
            }
        }

        var taskCreationResponse = await handler.HandleAsync(
            new TelegramUpdate(
                update.ChatId,
                CallbackData: TelegramCallbackData.CreateTask,
                Username: update.Username,
                FirstName: update.FirstName,
                LastName: update.LastName,
                SenderUserId: update.SenderUserId,
                MessageId: update.MessageId,
                MessageThreadId: messageThreadId,
                ReplyToMessageId: update.ReplyToMessageId,
                IsPrivateChat: update.IsPrivateChat),
            cancellationToken);
        await gateway.SendResponseAsync(update.ChatId, messageThreadId, taskCreationResponse, cancellationToken);

        return true;
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
        var runtimeEvents = await handler.ListTaskRuntimeEventsAsync(taskId, 5, cancellationToken);
        var stateFingerprint = CreateTaskStateFingerprint(task);
        taskWatches[key] = new TaskWatch(
            chatId,
            messageThreadId,
            taskId,
            stateFingerprint,
            CreateTaskWatchFingerprint(task, runtimeEvents),
            isDetails ? messageId : null,
            isDetails && messageId is not null);
        await TryUpdateTaskTopicTitleAsync(chatId, messageThreadId, task, cancellationToken);
        await handler.RecordTaskBindingAsync(
            chatId,
            messageThreadId,
            taskId,
            isDetails ? messageId : null,
            cancellationToken);
    }

    private async Task NotifyTaskWatchersAsync(CancellationToken cancellationToken)
    {
        await LoadDurableTaskWatchesAsync(cancellationToken);

        foreach (var pair in taskWatches.ToArray())
        {
            var task = await handler.GetTaskOrDefaultAsync(pair.Key.TaskId, cancellationToken);

            if (task is null)
            {
                taskWatches.TryRemove(pair.Key, out _);
                await handler.ForgetTaskBindingAsync(
                    pair.Key.ChatId,
                    pair.Key.MessageThreadId,
                    pair.Key.TaskId,
                    cancellationToken);
                continue;
            }

            var runtimeEvents = await handler.ListTaskRuntimeEventsAsync(task.Id, 5, cancellationToken);
            var stateFingerprint = CreateTaskStateFingerprint(task);
            var fingerprint = CreateTaskWatchFingerprint(task, runtimeEvents);

            if (fingerprint == pair.Value.LastFingerprint)
            {
                continue;
            }

            await TryUpdateTaskTopicTitleAsync(pair.Key.ChatId, pair.Key.MessageThreadId, task, cancellationToken);

            if (pair.Value.LastBotMessageIsTaskDetails && pair.Value.DetailMessageId is not null)
            {
                try
                {
                    var response = await handler.TokenizeResponseAsync(
                        pair.Key.ChatId,
                        await handler.RenderTaskDetailsAsync(
                            task.Id,
                            cancellationToken,
                            includeTerminalNavigation: pair.Key.MessageThreadId is null),
                        cancellationToken);
                    await gateway.EditResponseAsync(pair.Key.ChatId, pair.Value.DetailMessageId.Value, response, cancellationToken);
                    taskWatches[pair.Key] = pair.Value with
                    {
                        LastStateFingerprint = stateFingerprint,
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

            if (stateFingerprint == pair.Value.LastStateFingerprint)
            {
                taskWatches[pair.Key] = pair.Value with
                {
                    LastFingerprint = fingerprint,
                };

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
                    LastStateFingerprint = stateFingerprint,
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

    private async Task TryUpdateTaskTopicTitleAsync(
        long chatId,
        int? messageThreadId,
        RuntimeTask task,
        CancellationToken cancellationToken)
    {
        if (messageThreadId is null)
        {
            return;
        }

        try
        {
            await gateway.UpdateForumTopicTitleAsync(
                chatId,
                messageThreadId.Value,
                CreateTaskTopicTitle(task),
                cancellationToken);
        }
        catch (Exception exception) when (exception is RequestException or ApiRequestException)
        {
            await LogAsync(
                new TelegramLongPollingLogEntry(
                    TelegramLongPollingLogLevel.Warning,
                    "Telegram forum topic title update failed.",
                    ExceptionType: exception.GetType().Name,
                    ErrorMessage: exception.Message),
                cancellationToken);
        }
    }

    private async Task LoadDurableTaskWatchesAsync(CancellationToken cancellationToken)
    {
        var bindings = await handler.ListTaskBindingsAsync(cancellationToken);

        foreach (var binding in bindings)
        {
            var key = new TaskWatchKey(binding.ChatId, binding.MessageThreadId, binding.TaskId);

            if (taskWatches.ContainsKey(key))
            {
                continue;
            }

            var task = await handler.GetTaskOrDefaultAsync(binding.TaskId, cancellationToken);
            if (task is null || IsTerminal(task.Status))
            {
                await handler.ForgetTaskBindingAsync(
                    binding.ChatId,
                    binding.MessageThreadId,
                    binding.TaskId,
                    cancellationToken);
                continue;
            }

            taskWatches[key] = new TaskWatch(
                binding.ChatId,
                binding.MessageThreadId,
                binding.TaskId,
                CreateTaskStateFingerprint(task),
                CreateTaskWatchFingerprint(
                    task,
                    await handler.ListTaskRuntimeEventsAsync(task.Id, 5, cancellationToken)),
                binding.DetailMessageId,
                binding.DetailMessageId is not null);
        }
    }

    private async ValueTask ForgetMigratedTaskWatchAsync(
        TaskWatchKey key,
        Exception exception,
        CancellationToken cancellationToken)
    {
        taskWatches.TryRemove(key, out _);
        await handler.ForgetTaskBindingAsync(
            key.ChatId,
            key.MessageThreadId,
            key.TaskId,
            cancellationToken);
        await LogAsync(
            new TelegramLongPollingLogEntry(
                TelegramLongPollingLogLevel.Warning,
                "Telegram chat migrated to a supergroup; removing task watch for the old chat id.",
                ExceptionType: exception.GetType().Name,
                ErrorMessage: exception.Message),
            cancellationToken);
    }

    private async Task TryAnswerCallbackQueryAsync(
        string callbackQueryId,
        TelegramBotUpdate update,
        int processed,
        CancellationToken cancellationToken)
    {
        try
        {
            await gateway.AnswerCallbackQueryAsync(callbackQueryId, cancellationToken);
        }
        catch (Exception exception) when (IsTelegramStaleCallbackException(exception))
        {
            await LogAsync(
                new TelegramLongPollingLogEntry(
                    TelegramLongPollingLogLevel.Warning,
                    "Telegram callback query acknowledgement is stale; processing callback payload anyway.",
                    NextOffset: update.UpdateId + 1,
                    ProcessedUpdates: processed + 1,
                    ExceptionType: exception.GetType().Name,
                    ErrorMessage: exception.Message),
                cancellationToken);
        }
    }

    private TelegramUpdate ToInteractionUpdate(TelegramBotUpdate update) =>
        new(
            update.ChatId,
            update.Text,
            update.CallbackData,
            update.Username,
            update.FirstName,
            update.LastName,
            update.SenderUserId,
            update.MessageId,
            GetRoutingThreadId(update),
            update.ReplyToMessageId,
            update.IsPrivateChat);

    private int? GetRoutingThreadId(TelegramBotUpdate update)
    {
        if (!update.IsPrivateChat)
        {
            return update.MessageThreadId;
        }

        return handler.GetRoutingThreadId(ToRawInteractionUpdate(update));
    }

    private static TelegramUpdate ToRawInteractionUpdate(TelegramBotUpdate update) =>
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

    private static string FormatUpdateForLog(TelegramBotUpdate update, int? routingThreadId)
    {
        var kind = update.CallbackData is not null
            ? "callback"
            : update.Text is not null ? "text" : "other";
        var callback = update.CallbackData is null
            ? "(none)"
            : SanitizeCallbackForLog(update.CallbackData);
        var hasText = update.Text is null ? "false" : "true";

        return
            $"Processing Telegram update. updateId={update.UpdateId} chatId={update.ChatId} " +
            $"messageThreadId={FormatOptionalInt(update.MessageThreadId)} routingThreadId={FormatOptionalInt(routingThreadId)} " +
            $"senderUserId={FormatOptionalLong(update.SenderUserId)} kind={kind} callback={callback} hasText={hasText}";
    }

    private static string SanitizeCallbackForLog(string callbackData)
    {
        if (callbackData.StartsWith("a:", StringComparison.Ordinal))
        {
            return "token";
        }

        var parts = callbackData.Split(':', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length <= 3
            ? callbackData
            : string.Join(':', parts.Take(3)) + ":...";
    }

    private static string FormatOptionalInt(int? value) =>
        value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)";

    private static string FormatOptionalLong(long? value) =>
        value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)";

    private static string CreateTaskStateFingerprint(RuntimeTask task) =>
        $"{task.Status.ToStorageValue()}:{task.CurrentIteration}:{task.FailureReason}";

    private static string CreateTaskWatchFingerprint(
        RuntimeTask task,
        IReadOnlyList<RuntimeEvent> runtimeEvents)
    {
        var latestEvent = runtimeEvents
            .OrderByDescending(runtimeEvent => runtimeEvent.CreatedAt)
            .ThenByDescending(runtimeEvent => runtimeEvent.Id.Value)
            .FirstOrDefault();

        return $"{CreateTaskStateFingerprint(task)}:{latestEvent?.Id.Value ?? "none"}";
    }

    private static string CreateTaskTopicTitle(RuntimeTask task)
    {
        var title = $"[{task.Status.ToStorageValue()}] {task.Title}".Trim();

        return title.Length <= 128 ? title : title[..128];
    }

    private static bool IsTerminal(RuntimeTaskStatus status) =>
        status is RuntimeTaskStatus.Completed or RuntimeTaskStatus.Failed or RuntimeTaskStatus.Cancelled;

    private static bool TryParseNewTaskCommand(
        string text,
        string? botUsername,
        out string topicTitle)
    {
        topicTitle = "Aeges task";
        var trimmed = text.Trim();

        if (string.IsNullOrWhiteSpace(botUsername))
        {
            return false;
        }

        var mention = $"@{botUsername}";
        if (!trimmed.StartsWith(mention, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var command = trimmed[mention.Length..].TrimStart();
        var titleStart = MatchTaskCommand(command);
        if (titleStart is null)
        {
            return false;
        }

        var title = command[titleStart.Value..].TrimStart(' ', '\t', ':', '-');
        if (!string.IsNullOrWhiteSpace(title))
        {
            topicTitle = title.Length <= 96 ? title : title[..96];
        }

        return true;
    }

    private static int? MatchTaskCommand(string command)
    {
        foreach (var candidate in new[] { "new task", "task" })
        {
            if (!command.StartsWith(candidate, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (command.Length > candidate.Length
                && !char.IsWhiteSpace(command[candidate.Length])
                && command[candidate.Length] != ':')
            {
                continue;
            }

            return candidate.Length;
        }

        return null;
    }

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
        string LastStateFingerprint,
        string LastFingerprint,
        int? DetailMessageId,
        bool LastBotMessageIsTaskDetails);
}
