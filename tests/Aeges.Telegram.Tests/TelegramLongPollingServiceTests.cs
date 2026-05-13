using Aeges.Application.Configuration;
using Aeges.Application.TelegramUsers;
using Aeges.Core;
using Aeges.Telegram;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace Aeges.Telegram.Tests;

public sealed class TelegramLongPollingServiceTests
{
    [Fact]
    public async Task PollOnceAsync_answers_callback_and_edits_handler_response()
    {
        var gateway = new FakeTelegramBotGateway
        {
            Updates =
            [
                new TelegramBotUpdate(
                    41,
                    1001,
                    Text: null,
                    CallbackData: TelegramCallbackData.ListQueuedTasks,
                    CallbackQueryId: "callback-001",
                    MessageId: 9001),
            ],
        };
        var service = CreateService(gateway);

        var result = await service.PollOnceAsync(
            nextOffset: null,
            new TelegramLongPollingOptions(Limit: 25, TimeoutSeconds: 5),
            CancellationToken.None);

        Assert.Equal(42, result.NextOffset);
        Assert.Equal(1, result.ProcessedUpdates);
        Assert.Equal(["callback-001"], gateway.AnsweredCallbackQueryIds);
        Assert.Empty(gateway.SentResponses);
        Assert.Single(gateway.EditedResponses);
        Assert.Equal(9001, gateway.EditedResponses[0].MessageId);
        Assert.Equal("No queued tasks.", gateway.EditedResponses[0].Response.Text);
        Assert.Equal(25, gateway.LastLimit);
        Assert.Equal(5, gateway.LastTimeoutSeconds);
    }

    [Fact]
    public async Task PollOnceAsync_sends_pending_message_then_edits_it_for_text_response()
    {
        var gateway = new FakeTelegramBotGateway
        {
            Updates =
            [
                new TelegramBotUpdate(
                    41,
                    1001,
                    Text: "hello",
                    CallbackData: null,
                    CallbackQueryId: null),
            ],
        };
        var service = CreateService(gateway);

        var result = await service.PollOnceAsync(
            nextOffset: null,
            new TelegramLongPollingOptions(),
            CancellationToken.None);

        Assert.Equal(42, result.NextOffset);
        Assert.Equal(1, result.ProcessedUpdates);
        Assert.Single(gateway.SentResponses);
        Assert.Contains("Working on your message", gateway.SentResponses[0].Response.Text, StringComparison.Ordinal);
        Assert.Equal("Cancel", gateway.SentResponses[0].Response.Buttons.Rows[0][0].Text);
        Assert.Equal(TelegramCallbackData.CancelPendingTextResponse, gateway.SentResponses[0].Response.Buttons.Rows[0][0].CallbackData);
        Assert.Single(gateway.EditedResponses);
        Assert.Equal(1, gateway.EditedResponses[0].MessageId);
        Assert.Equal("talk_unavailable: Talk is not available in this test facade.", gateway.EditedResponses[0].Response.Text);
    }

    [Fact]
    public async Task PollOnceAsync_sends_text_response_to_original_forum_topic()
    {
        var gateway = new FakeTelegramBotGateway
        {
            Updates =
            [
                new TelegramBotUpdate(
                    41,
                    -1001,
                    Text: "hello",
                    CallbackData: null,
                    CallbackQueryId: null,
                    MessageThreadId: 77,
                    IsPrivateChat: false),
            ],
        };
        var service = CreateService(gateway);

        await service.PollOnceAsync(
            nextOffset: null,
            new TelegramLongPollingOptions(),
            CancellationToken.None);

        Assert.Single(gateway.SentResponses);
        Assert.Equal(77, gateway.SentResponses[0].MessageThreadId);
        Assert.Single(gateway.EditedResponses);
    }

    [Fact]
    public async Task PollOnceAsync_creates_forum_topic_for_new_task_command_in_supergroup()
    {
        var gateway = new FakeTelegramBotGateway
        {
            Updates =
            [
                new TelegramBotUpdate(
                    41,
                    -1001,
                    Text: "@aeges_test_bot new task Fix install docs",
                    CallbackData: null,
                    CallbackQueryId: null,
                    Username: "operator",
                    SenderUserId: 1001,
                    IsPrivateChat: false),
            ],
        };
        var service = CreateService(gateway);

        var result = await service.PollOnceAsync(
            nextOffset: null,
            new TelegramLongPollingOptions(),
            CancellationToken.None);

        Assert.Equal(42, result.NextOffset);
        Assert.Equal(1, result.ProcessedUpdates);
        Assert.Single(gateway.CreatedTopics);
        Assert.Equal((-1001, "Fix install docs"), gateway.CreatedTopics[0]);
        Assert.Single(gateway.SentResponses);
        Assert.Equal(777, gateway.SentResponses[0].MessageThreadId);
        Assert.Contains("No active projects", gateway.SentResponses[0].Response.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PollOnceAsync_creates_forum_topic_for_short_task_command_in_supergroup()
    {
        var gateway = new FakeTelegramBotGateway
        {
            Updates =
            [
                new TelegramBotUpdate(
                    41,
                    -1001,
                    Text: "@aeges_test_bot task: Test task thread",
                    CallbackData: null,
                    CallbackQueryId: null,
                    Username: "operator",
                    SenderUserId: 1001,
                    IsPrivateChat: false),
            ],
        };
        var service = CreateService(gateway);

        var result = await service.PollOnceAsync(
            nextOffset: null,
            new TelegramLongPollingOptions(),
            CancellationToken.None);

        Assert.Equal(42, result.NextOffset);
        Assert.Equal(1, result.ProcessedUpdates);
        Assert.Single(gateway.CreatedTopics);
        Assert.Equal((-1001, "Test task thread"), gateway.CreatedTopics[0]);
        Assert.Single(gateway.SentResponses);
        Assert.Equal(777, gateway.SentResponses[0].MessageThreadId);
        Assert.Contains("No active projects", gateway.SentResponses[0].Response.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PollOnceAsync_sends_menu_without_pending_message_for_start_command()
    {
        var gateway = new FakeTelegramBotGateway
        {
            Updates =
            [
                new TelegramBotUpdate(
                    41,
                    1001,
                    Text: "/start",
                    CallbackData: null,
                    CallbackQueryId: null),
            ],
        };
        var service = CreateService(gateway);

        var result = await service.PollOnceAsync(
            nextOffset: null,
            new TelegramLongPollingOptions(),
            CancellationToken.None);

        Assert.Equal(42, result.NextOffset);
        Assert.Equal(1, result.ProcessedUpdates);
        Assert.Single(gateway.SentResponses);
        Assert.Empty(gateway.EditedResponses);
        Assert.Equal("Aeges control", gateway.SentResponses[0].Response.Text);
    }

    [Fact]
    public async Task PollOnceAsync_keeps_offset_when_no_updates_arrive()
    {
        var gateway = new FakeTelegramBotGateway();
        var service = CreateService(gateway);

        var result = await service.PollOnceAsync(
            100,
            new TelegramLongPollingOptions(),
            CancellationToken.None);

        Assert.Equal(100, result.NextOffset);
        Assert.Equal(0, result.ProcessedUpdates);
        Assert.Empty(gateway.SentResponses);
    }

    [Fact]
    public async Task PollOnceAsync_edits_task_details_when_watched_task_changes()
    {
        var task = RuntimeTask.Create(
            new TaskId("task-001"),
            new ProjectId("project-aeges"),
            new MachineId("machine-local"),
            "Wire notifications",
            "Notify the chat when task status changes.",
            DateTimeOffset.UtcNow);
        var facade = new FakeTelegramApplicationFacade { WatchedTask = task };
        var gateway = new FakeTelegramBotGateway
        {
            Updates =
            [
                new TelegramBotUpdate(
                    41,
                    1001,
                    Text: null,
                    CallbackData: TelegramCallbackData.ViewTask(task.Id),
                    CallbackQueryId: "callback-001",
                    MessageId: 9001),
            ],
        };
        var service = CreateService(gateway, facade);

        await service.PollOnceAsync(null, new TelegramLongPollingOptions(), CancellationToken.None);
        task.StartPlanning(DateTimeOffset.UtcNow);
        gateway.Updates = [];

        await service.PollOnceAsync(42, new TelegramLongPollingOptions(), CancellationToken.None);

        Assert.Equal(2, gateway.EditedResponses.Count);
        Assert.Equal(9001, gateway.EditedResponses[1].MessageId);
        Assert.Contains("Status: planning", gateway.EditedResponses[1].Response.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PollOnceAsync_edits_task_details_when_runner_progress_changes()
    {
        var task = RuntimeTask.Create(
            new TaskId("task-001"),
            new ProjectId("project-aeges"),
            new MachineId("machine-local"),
            "Wire progress",
            "Notify the chat when runner progress changes.",
            DateTimeOffset.UtcNow);
        var runtimeEvents = new List<RuntimeEvent>();
        var facade = new FakeTelegramApplicationFacade
        {
            WatchedTask = task,
            RuntimeEvents = runtimeEvents,
        };
        var gateway = new FakeTelegramBotGateway
        {
            Updates =
            [
                new TelegramBotUpdate(
                    41,
                    1001,
                    Text: null,
                    CallbackData: TelegramCallbackData.ViewTask(task.Id),
                    CallbackQueryId: "callback-001",
                    MessageId: 9001),
            ],
        };
        var service = CreateService(gateway, facade);

        await service.PollOnceAsync(null, new TelegramLongPollingOptions(), CancellationToken.None);
        runtimeEvents.Add(RuntimeEvent.Create(
            new RuntimeEventId("runtime-event-001"),
            task.Id,
            new IterationId("iteration-001"),
            new MachineId("machine-local"),
            "runner.message",
            "Codex finished the first file.",
            null,
            DateTimeOffset.UtcNow));
        gateway.Updates = [];

        await service.PollOnceAsync(42, new TelegramLongPollingOptions(), CancellationToken.None);

        Assert.Equal(2, gateway.EditedResponses.Count);
        Assert.Equal(9001, gateway.EditedResponses[1].MessageId);
        Assert.Contains("Runner progress:", gateway.EditedResponses[1].Response.Text, StringComparison.Ordinal);
        Assert.Contains("Codex finished the first file.", gateway.EditedResponses[1].Response.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PollOnceAsync_sends_notification_when_changed_task_is_not_last_details_message()
    {
        var task = RuntimeTask.Create(
            new TaskId("task-001"),
            new ProjectId("project-aeges"),
            new MachineId("machine-local"),
            "Wire notifications",
            "Notify the chat when task status changes.",
            DateTimeOffset.UtcNow);
        var facade = new FakeTelegramApplicationFacade { WatchedTask = task };
        var gateway = new FakeTelegramBotGateway
        {
            Updates =
            [
                new TelegramBotUpdate(
                    41,
                    1001,
                    Text: null,
                    CallbackData: TelegramCallbackData.ViewTask(task.Id),
                    CallbackQueryId: "callback-001",
                    MessageId: 9001),
            ],
        };
        var service = CreateService(gateway, facade);

        await service.PollOnceAsync(null, new TelegramLongPollingOptions(), CancellationToken.None);
        gateway.Updates =
        [
            new TelegramBotUpdate(42, 1001, Text: "menu", CallbackData: null, CallbackQueryId: null),
        ];
        await service.PollOnceAsync(42, new TelegramLongPollingOptions(), CancellationToken.None);
        task.StartPlanning(DateTimeOffset.UtcNow);
        gateway.Updates = [];

        await service.PollOnceAsync(43, new TelegramLongPollingOptions(), CancellationToken.None);

        Assert.Equal(2, gateway.SentResponses.Count);
        Assert.Contains("Task updated: task-001", gateway.SentResponses[1].Response.Text, StringComparison.Ordinal);
        Assert.Contains("Status: planning", gateway.SentResponses[1].Response.Text, StringComparison.Ordinal);
        Assert.Equal(TelegramCallbackData.ViewTask(task.Id), gateway.SentResponses[1].Response.Buttons.Rows[0][0].CallbackData);
    }

    [Fact]
    public async Task PollOnceAsync_does_not_send_extra_notification_after_completion_callback()
    {
        var task = RuntimeTask.Create(
            new TaskId("task-001"),
            new ProjectId("project-aeges"),
            new MachineId("machine-local"),
            "Complete from Telegram",
            "Complete without a duplicate notification.",
            DateTimeOffset.UtcNow);
        task.StartPlanning(DateTimeOffset.UtcNow);
        task.StartRunning(DateTimeOffset.UtcNow);
        task.StartReview(DateTimeOffset.UtcNow);
        var facade = new FakeTelegramApplicationFacade { WatchedTask = task };
        var gateway = new FakeTelegramBotGateway
        {
            Updates =
            [
                new TelegramBotUpdate(
                    41,
                    1001,
                    Text: null,
                    CallbackData: TelegramCallbackData.ViewTask(task.Id),
                    CallbackQueryId: "callback-001",
                    MessageId: 9001),
            ],
        };
        var service = CreateService(gateway, facade);

        await service.PollOnceAsync(null, new TelegramLongPollingOptions(), CancellationToken.None);
        gateway.Updates =
        [
            new TelegramBotUpdate(
                42,
                1001,
                Text: null,
                CallbackData: TelegramCallbackData.CompleteTask(task.Id),
                CallbackQueryId: "callback-002",
                MessageId: 9001),
        ];

        await service.PollOnceAsync(42, new TelegramLongPollingOptions(), CancellationToken.None);

        Assert.Equal(RuntimeTaskStatus.Completed, task.Status);
        Assert.Empty(gateway.SentResponses);
        Assert.Equal(2, gateway.EditedResponses.Count);
        Assert.Contains("Task completed: task-001", gateway.EditedResponses[1].Response.Text, StringComparison.Ordinal);
        Assert.Contains("Aeges control", gateway.EditedResponses[1].Response.Text, StringComparison.Ordinal);
        Assert.Contains(gateway.EditedResponses[1].Response.Buttons.Rows.SelectMany(row => row), button => button.Text == "New task");
    }

    [Fact]
    public async Task PollOnceAsync_keeps_topic_task_message_without_buttons_after_completion_callback()
    {
        var task = RuntimeTask.Create(
            new TaskId("task-001"),
            new ProjectId("project-aeges"),
            new MachineId("machine-local"),
            "Complete from Telegram",
            "Complete without a duplicate notification.",
            DateTimeOffset.UtcNow);
        task.StartPlanning(DateTimeOffset.UtcNow);
        task.StartRunning(DateTimeOffset.UtcNow);
        task.StartReview(DateTimeOffset.UtcNow);
        var facade = new FakeTelegramApplicationFacade { WatchedTask = task };
        var gateway = new FakeTelegramBotGateway
        {
            Updates =
            [
                new TelegramBotUpdate(
                    41,
                    -1001,
                    Text: null,
                    CallbackData: TelegramCallbackData.ViewTask(task.Id),
                    CallbackQueryId: "callback-001",
                    MessageId: 9001,
                    MessageThreadId: 77),
            ],
        };
        var service = CreateService(gateway, facade);

        await service.PollOnceAsync(null, new TelegramLongPollingOptions(), CancellationToken.None);
        gateway.Updates =
        [
            new TelegramBotUpdate(
                42,
                -1001,
                Text: null,
                CallbackData: TelegramCallbackData.CompleteTask(task.Id),
                CallbackQueryId: "callback-002",
                MessageId: 9001,
                MessageThreadId: 77),
        ];

        await service.PollOnceAsync(42, new TelegramLongPollingOptions(), CancellationToken.None);

        Assert.Equal(RuntimeTaskStatus.Completed, task.Status);
        Assert.Empty(gateway.SentResponses);
        Assert.Equal(2, gateway.EditedResponses.Count);
        Assert.Contains("> Status: completed", gateway.EditedResponses[1].Response.Text, StringComparison.Ordinal);
        Assert.Empty(gateway.EditedResponses[1].Response.Buttons.Rows);
    }

    [Fact]
    public async Task PollOnceAsync_updates_forum_topic_title_when_watched_task_status_changes()
    {
        var task = RuntimeTask.Create(
            new TaskId("task-001"),
            new ProjectId("project-aeges"),
            new MachineId("machine-local"),
            "Complete from Telegram",
            "Update topic title when task changes.",
            DateTimeOffset.UtcNow);
        var facade = new FakeTelegramApplicationFacade { WatchedTask = task };
        var gateway = new FakeTelegramBotGateway
        {
            Updates =
            [
                new TelegramBotUpdate(
                    41,
                    -1001,
                    Text: null,
                    CallbackData: TelegramCallbackData.ViewTask(task.Id),
                    CallbackQueryId: "callback-001",
                    MessageId: 9001,
                    MessageThreadId: 77,
                    IsPrivateChat: false),
            ],
        };
        var service = CreateService(gateway, facade);

        await service.PollOnceAsync(null, new TelegramLongPollingOptions(), CancellationToken.None);
        task.StartPlanning(DateTimeOffset.UtcNow);
        gateway.Updates = [];

        await service.PollOnceAsync(42, new TelegramLongPollingOptions(), CancellationToken.None);

        Assert.Contains(gateway.UpdatedTopics, topic => topic == (-1001, 77, "[queued] Complete from Telegram"));
        Assert.Contains(gateway.UpdatedTopics, topic => topic == (-1001, 77, "[planning] Complete from Telegram"));
    }

    [Fact]
    public async Task RunAsync_continues_after_transient_polling_failure()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var logs = new List<TelegramLongPollingLogEntry>();
        var gateway = new FakeTelegramBotGateway
        {
            ThrowTransientPollingFailureOnce = true,
            Updates =
            [
                new TelegramBotUpdate(
                    41,
                    1001,
                    Text: "hello",
                    CallbackData: null,
                    CallbackQueryId: null),
            ],
        };
        gateway.ResponseEdited += (_, _) => cancellation.Cancel();
        var service = CreateService(gateway, transientErrorDelay: TimeSpan.Zero, logs: logs);

        await service.RunAsync(new TelegramLongPollingOptions(), cancellation.Token);

        Assert.Equal(2, gateway.GetUpdatesCallCount);
        Assert.Single(gateway.SentResponses);
        Assert.Single(gateway.EditedResponses);
        Assert.Contains("Working on your message", gateway.SentResponses[0].Response.Text, StringComparison.Ordinal);
        Assert.Equal("talk_unavailable: Talk is not available in this test facade.", gateway.EditedResponses[0].Response.Text);
        Assert.Contains(logs, entry =>
            entry.Level == TelegramLongPollingLogLevel.Warning
            && entry.Message.Contains("retrying", StringComparison.Ordinal)
            && entry.ExceptionType == nameof(HttpRequestException));
        Assert.Contains(logs, entry =>
            entry.Level == TelegramLongPollingLogLevel.Information
            && entry.ProcessedUpdates == 1);
    }

    [Fact]
    public async Task RunAsync_does_not_log_empty_polling_batches()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var logs = new List<TelegramLongPollingLogEntry>();
        var gateway = new FakeTelegramBotGateway();
        gateway.BeforeGetUpdates += (_, _) =>
        {
            if (gateway.GetUpdatesCallCount == 2)
            {
                cancellation.Cancel();
            }
        };
        var service = CreateService(gateway, transientErrorDelay: TimeSpan.Zero, logs: logs);

        await service.RunAsync(new TelegramLongPollingOptions(), cancellation.Token);

        Assert.Empty(logs);
    }

    [Fact]
    public async Task PollOnceAsync_skips_group_migration_errors_without_retrying_forever()
    {
        var logs = new List<TelegramLongPollingLogEntry>();
        var gateway = new FakeTelegramBotGateway
        {
            ThrowChatMigrationOnSend = true,
            Updates =
            [
                new TelegramBotUpdate(
                    41,
                    -1001,
                    Text: "/start",
                    CallbackData: null,
                    CallbackQueryId: null,
                    IsPrivateChat: false),
            ],
        };
        var service = CreateService(gateway, transientErrorDelay: TimeSpan.Zero, logs: logs);

        var result = await service.PollOnceAsync(null, new TelegramLongPollingOptions(), CancellationToken.None);

        Assert.Equal(42, result.NextOffset);
        Assert.Equal(1, result.ProcessedUpdates);
        Assert.Single(gateway.SentResponses);
        Assert.Contains(logs, entry =>
            entry.Level == TelegramLongPollingLogLevel.Warning
            && entry.Message.Contains("migrated", StringComparison.OrdinalIgnoreCase)
            && entry.ExceptionType == nameof(ApiRequestException));
    }

    [Fact]
    public async Task PollOnceAsync_removes_task_watch_when_chat_migrates()
    {
        var logs = new List<TelegramLongPollingLogEntry>();
        var task = RuntimeTask.Create(
            new TaskId("task-001"),
            new ProjectId("project-aeges"),
            new MachineId("machine-local"),
            "Wire notifications",
            "Notify the chat when task status changes.",
            DateTimeOffset.UtcNow);
        var facade = new FakeTelegramApplicationFacade { WatchedTask = task };
        var gateway = new FakeTelegramBotGateway
        {
            Updates =
            [
                new TelegramBotUpdate(
                    41,
                    -1001,
                    Text: null,
                    CallbackData: TelegramCallbackData.ViewTask(task.Id),
                    CallbackQueryId: "callback-001",
                    MessageId: 9001),
            ],
        };
        var service = CreateService(gateway, facade, logs: logs);

        await service.PollOnceAsync(null, new TelegramLongPollingOptions(), CancellationToken.None);
        task.StartPlanning(DateTimeOffset.UtcNow);
        gateway.Updates = [];
        gateway.ThrowChatMigrationOnEdit = true;

        await service.PollOnceAsync(42, new TelegramLongPollingOptions(), CancellationToken.None);

        gateway.ThrowChatMigrationOnEdit = false;
        task.StartRunning(DateTimeOffset.UtcNow);
        await service.PollOnceAsync(42, new TelegramLongPollingOptions(), CancellationToken.None);

        Assert.Equal(2, gateway.EditedResponses.Count);
        Assert.Contains(logs, entry =>
            entry.Level == TelegramLongPollingLogLevel.Warning
            && entry.Message.Contains("removing task watch", StringComparison.OrdinalIgnoreCase)
            && entry.ExceptionType == nameof(ApiRequestException));
    }

    [Fact]
    public async Task PollOnceAsync_reloads_durable_task_binding_after_restart()
    {
        var task = RuntimeTask.Create(
            new TaskId("task-001"),
            new ProjectId("project-aeges"),
            new MachineId("machine-local"),
            "Wire notifications",
            "Notify the topic when task status changes.",
            DateTimeOffset.UtcNow);
        var facade = new FakeTelegramApplicationFacade { WatchedTask = task };
        facade.TaskBindings.Add(RuntimeTelegramTaskBinding.Create(
            -1001,
            77,
            task.Id,
            detailMessageId: 9001,
            DateTimeOffset.UtcNow));
        var gateway = new FakeTelegramBotGateway();
        var service = CreateService(gateway, facade);

        await service.PollOnceAsync(null, new TelegramLongPollingOptions(), CancellationToken.None);
        task.StartPlanning(DateTimeOffset.UtcNow);

        await service.PollOnceAsync(42, new TelegramLongPollingOptions(), CancellationToken.None);

        Assert.Single(gateway.EditedResponses);
        Assert.Equal(-1001, gateway.EditedResponses[0].ChatId);
        Assert.Equal(9001, gateway.EditedResponses[0].MessageId);
        Assert.Contains("Status: planning", gateway.EditedResponses[0].Response.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PollOnceAsync_processes_callback_payload_when_acknowledgement_is_stale()
    {
        var logs = new List<TelegramLongPollingLogEntry>();
        var gateway = new FakeTelegramBotGateway
        {
            ThrowStaleCallbackOnAnswer = true,
            Updates =
            [
                new TelegramBotUpdate(
                    41,
                    1001,
                    Text: null,
                    CallbackData: TelegramCallbackData.MainMenu,
                    CallbackQueryId: "callback-001",
                    MessageId: 9001),
                new TelegramBotUpdate(
                    42,
                    1001,
                    Text: "/start",
                    CallbackData: null,
                    CallbackQueryId: null),
            ],
        };
        var service = CreateService(gateway, transientErrorDelay: TimeSpan.Zero, logs: logs);

        var result = await service.PollOnceAsync(null, new TelegramLongPollingOptions(), CancellationToken.None);

        Assert.Equal(43, result.NextOffset);
        Assert.Equal(2, result.ProcessedUpdates);
        Assert.Equal(["callback-001"], gateway.AnsweredCallbackQueryIds);
        Assert.Single(gateway.SentResponses);
        Assert.Single(gateway.EditedResponses);
        Assert.Equal(9001, gateway.EditedResponses[0].MessageId);
        Assert.Equal("Aeges control", gateway.EditedResponses[0].Response.Text);
        Assert.Contains(logs, entry =>
            entry.Level == TelegramLongPollingLogLevel.Warning
            && entry.Message.Contains("stale", StringComparison.OrdinalIgnoreCase)
            && entry.ExceptionType == nameof(ApiRequestException));
    }

    [Fact]
    public async Task RunAsync_stops_cleanly_when_cancelled()
    {
        using var cancellation = new CancellationTokenSource();
        var gateway = new FakeTelegramBotGateway();
        gateway.BeforeGetUpdates += (_, _) => cancellation.Cancel();
        var service = CreateService(gateway, transientErrorDelay: TimeSpan.Zero);

        await service.RunAsync(new TelegramLongPollingOptions(), cancellation.Token);

        Assert.Equal(1, gateway.GetUpdatesCallCount);
    }

    private static TelegramLongPollingService CreateService(
        FakeTelegramBotGateway gateway,
        FakeTelegramApplicationFacade? facade = null,
        TimeSpan? transientErrorDelay = null,
        List<TelegramLongPollingLogEntry>? logs = null)
    {
        var handler = new TelegramInteractionHandler(
            facade ?? new FakeTelegramApplicationFacade(),
            new AegesTelegramConfiguration());

        return new TelegramLongPollingService(
            gateway,
            handler,
            transientErrorDelay,
            logs is null
                ? null
                : (entry, _) =>
                {
                    logs.Add(entry);
                    return ValueTask.CompletedTask;
                });
    }

    private sealed class FakeTelegramBotGateway : ITelegramBotGateway
    {
        public IReadOnlyList<TelegramBotUpdate> Updates { get; set; } = [];

        public bool ThrowTransientPollingFailureOnce { get; set; }

        public bool ThrowChatMigrationOnSend { get; set; }

        public bool ThrowChatMigrationOnEdit { get; set; }

        public bool ThrowStaleCallbackOnAnswer { get; set; }

        public int GetUpdatesCallCount { get; private set; }

        public event EventHandler? BeforeGetUpdates;

        public event EventHandler? ResponseSent;

        public event EventHandler? ResponseEdited;

        public List<string> AnsweredCallbackQueryIds { get; } = [];

        public List<(long ChatId, int? MessageThreadId, TelegramResponse Response)> SentResponses { get; } = [];

        public List<(long ChatId, int MessageId, TelegramResponse Response)> EditedResponses { get; } = [];

        public List<(long ChatId, string Name)> CreatedTopics { get; } = [];

        public List<(long ChatId, int MessageThreadId, string Name)> UpdatedTopics { get; } = [];

        public int? LastLimit { get; private set; }

        public int? LastTimeoutSeconds { get; private set; }

        public Task<TelegramBotIdentity> GetIdentityAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new TelegramBotIdentity(100, "aeges_test_bot", "Aeges Test", IsBot: true));

        public Task<IReadOnlyList<TelegramBotUpdate>> GetUpdatesAsync(
            int? offset,
            int limit,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            GetUpdatesCallCount++;
            BeforeGetUpdates?.Invoke(this, EventArgs.Empty);
            cancellationToken.ThrowIfCancellationRequested();

            if (ThrowTransientPollingFailureOnce)
            {
                ThrowTransientPollingFailureOnce = false;
                throw new HttpRequestException("Transient polling failure.");
            }

            LastLimit = limit;
            LastTimeoutSeconds = timeoutSeconds;

            return Task.FromResult(Updates);
        }

        public Task<int?> SendResponseAsync(
            long chatId,
            int? messageThreadId,
            TelegramResponse response,
            CancellationToken cancellationToken)
        {
            SentResponses.Add((chatId, messageThreadId, response));
            ResponseSent?.Invoke(this, EventArgs.Empty);
            if (ThrowChatMigrationOnSend)
            {
                throw new ApiRequestException(
                    "Bad Request: group chat was upgraded to a supergroup chat",
                    400,
                    new ResponseParameters { MigrateToChatId = -1001234 });
            }

            return Task.FromResult<int?>(SentResponses.Count);
        }

        public Task<TelegramForumTopic> CreateForumTopicAsync(
            long chatId,
            string name,
            CancellationToken cancellationToken)
        {
            CreatedTopics.Add((chatId, name));
            return Task.FromResult(new TelegramForumTopic(777, name));
        }

        public Task UpdateForumTopicTitleAsync(
            long chatId,
            int messageThreadId,
            string name,
            CancellationToken cancellationToken)
        {
            UpdatedTopics.Add((chatId, messageThreadId, name));
            return Task.CompletedTask;
        }

        public Task EditResponseAsync(
            long chatId,
            int messageId,
            TelegramResponse response,
            CancellationToken cancellationToken)
        {
            EditedResponses.Add((chatId, messageId, response));
            ResponseEdited?.Invoke(this, EventArgs.Empty);
            if (ThrowChatMigrationOnEdit)
            {
                throw new ApiRequestException(
                    "Bad Request: group chat was upgraded to a supergroup chat",
                    400,
                    new ResponseParameters { MigrateToChatId = -1001234 });
            }

            return Task.CompletedTask;
        }

        public Task AnswerCallbackQueryAsync(
            string callbackQueryId,
            CancellationToken cancellationToken)
        {
            AnsweredCallbackQueryIds.Add(callbackQueryId);
            if (ThrowStaleCallbackOnAnswer)
            {
                throw new ApiRequestException(
                    "Bad Request: query is too old and response timeout expired or query ID is invalid",
                    400);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeTelegramApplicationFacade : ITelegramApplicationFacade
    {
        public RuntimeTask? WatchedTask { get; init; }

        public IReadOnlyList<RuntimeEvent> RuntimeEvents { get; init; } = [];

        public List<RuntimeTelegramTaskBinding> TaskBindings { get; } = [];

        public Task<TelegramUserAuthorization> EnsureTelegramUserAsync(
            long chatId,
            RuntimeTelegramUserProfile profile,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                new TelegramUserAuthorization(
                    RuntimeTelegramUser.CreateFirstAdmin(chatId, DateTimeOffset.UtcNow, profile),
                    IsFirstAdmin: false));

        public Task<IReadOnlyList<RuntimeTelegramUser>> ListTelegramUsersAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RuntimeTelegramUser>>([]);

        public Task<Aeges.Application.ApplicationResult<TelegramUserAccessSnapshot>> GetTelegramUserAccessAsync(
            TelegramUserId userId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<TelegramUserAccessSnapshot>.Failure(
                "telegram_user_not_found",
                $"Telegram user '{userId}' was not found."));

        public Task<Aeges.Application.ApplicationResult<RuntimeTelegramUser>> ApproveTelegramUserAsync(
            TelegramUserId userId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<RuntimeTelegramUser>.Failure(
                "telegram_user_not_found",
                $"Telegram user '{userId}' was not found."));

        public Task<Aeges.Application.ApplicationResult<RuntimeTelegramUser>> DenyTelegramUserAsync(
            TelegramUserId userId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<RuntimeTelegramUser>.Failure(
                "telegram_user_not_found",
                $"Telegram user '{userId}' was not found."));

        public Task<Aeges.Application.ApplicationResult<TelegramUserAccessSnapshot>> SetTelegramProjectAccessAsync(
            TelegramUserId userId,
            ProjectId projectId,
            bool allowed,
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<TelegramUserAccessSnapshot>.Failure(
                "telegram_user_not_found",
                $"Telegram user '{userId}' was not found."));

        public Task<Aeges.Application.ApplicationResult<TelegramUserAccessSnapshot>> SetTelegramProjectGroupAccessAsync(
            TelegramUserId userId,
            ProjectGroupId projectGroupId,
            bool allowed,
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<TelegramUserAccessSnapshot>.Failure(
                "telegram_user_not_found",
                $"Telegram user '{userId}' was not found."));

        public Task<IReadOnlyList<RuntimeProject>> ListProjectsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RuntimeProject>>([]);

        public Task<IReadOnlyList<RuntimeProjectGroup>> ListProjectGroupsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RuntimeProjectGroup>>([]);

        public Task<IReadOnlyList<RuntimeProject>> ListActiveProjectsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RuntimeProject>>([]);

        public Task<Aeges.Application.ApplicationResult<RuntimeProject>> GetProjectAsync(
            ProjectId projectId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<RuntimeProject>.Failure(
                "project_not_found",
                $"Project '{projectId}' was not found."));

        public Task<Aeges.Application.ApplicationResult<RuntimeProject>> ArchiveProjectAsync(
            ProjectId projectId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<RuntimeProject>.Failure(
                "project_not_found",
                $"Project '{projectId}' was not found."));

        public Task<Aeges.Application.ApplicationResult<Aeges.Application.Talk.TalkExchange>> SendTalkMessageAsync(
            long chatId,
            string message,
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<Aeges.Application.Talk.TalkExchange>.Failure(
                "talk_unavailable",
                "Talk is not available in this test facade."));

        public Task<IReadOnlyList<RuntimeMachine>> ListMachinesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RuntimeMachine>>([]);

        public Task<IReadOnlyList<RuntimeTask>> ListQueuedTasksAsync(
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RuntimeTask>>([]);

        public Task<IReadOnlyList<RuntimeTask>> ListTasksByStatusAsync(
            RuntimeTaskStatus status,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RuntimeTask>>(
                WatchedTask is not null && WatchedTask.Status == status ? [WatchedTask] : []);

        public Task<IReadOnlyList<RuntimeTask>> ListProjectTasksByStatusAsync(
            ProjectId projectId,
            RuntimeTaskStatus status,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RuntimeTask>>([]);

        public Task<IReadOnlyList<ApprovalRequest>> ListPendingApprovalsAsync(
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ApprovalRequest>>([]);

        public Task<TelegramRunnerSettings> GetRunnerSettingsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new TelegramRunnerSettings(
                "workspace-write",
                CodexBypassApprovalsAndSandbox: false,
                AgentMaxParallelTasks: 1));

        public Task<Aeges.Application.ApplicationResult<TelegramRunnerSettings>> SetCodexSandboxModeAsync(
            string sandboxMode,
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<TelegramRunnerSettings>.Success(
                new TelegramRunnerSettings(
                    sandboxMode,
                    CodexBypassApprovalsAndSandbox: false,
                    AgentMaxParallelTasks: 1)));

        public Task<Aeges.Application.ApplicationResult<TelegramRunnerSettings>> SetCodexBypassApprovalsAndSandboxAsync(
            bool enabled,
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<TelegramRunnerSettings>.Success(
                new TelegramRunnerSettings(
                    "workspace-write",
                    enabled,
                    AgentMaxParallelTasks: 1)));

        public Task<Aeges.Application.ApplicationResult<TelegramRunnerSettings>> SetAgentMaxParallelTasksAsync(
            int maxParallelTasks,
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<TelegramRunnerSettings>.Success(
                new TelegramRunnerSettings(
                    "workspace-write",
                    CodexBypassApprovalsAndSandbox: false,
                    maxParallelTasks)));

        public Task<Aeges.Application.ApplicationResult<TelegramAgentRestartResult>> RestartAgentAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<TelegramAgentRestartResult>.Success(
                new TelegramAgentRestartResult("Agent restarted.")));

        public Task<Aeges.Application.ApplicationResult<TelegramAgentRestartResult>> StartAgentAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<TelegramAgentRestartResult>.Success(
                new TelegramAgentRestartResult("Agent started.")));

        public Task<Aeges.Application.ApplicationResult<RuntimeTask>> CreateTaskAsync(
            ProjectId projectId,
            MachineId machineId,
            string title,
            string goal,
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<RuntimeTask>.Failure(
                "task_creation_unavailable",
                "Task creation is not available in this test facade."));

        public Task<Aeges.Application.ApplicationResult<RuntimeTask>> GetTaskAsync(
            TaskId taskId,
            CancellationToken cancellationToken)
        {
            var result = WatchedTask is not null && WatchedTask.Id == taskId
                ? Aeges.Application.ApplicationResult<RuntimeTask>.Success(WatchedTask)
                : Aeges.Application.ApplicationResult<RuntimeTask>.Failure(
                    "task_not_found",
                    $"Task '{taskId}' was not found.");

            return Task.FromResult(result);
        }

        public Task RecordTaskBindingAsync(
            long chatId,
            int? messageThreadId,
            TaskId taskId,
            int? detailMessageId,
            CancellationToken cancellationToken)
        {
            TaskBindings.RemoveAll(binding =>
                binding.ChatId == chatId
                && binding.MessageThreadId == messageThreadId
                && binding.TaskId == taskId);
            TaskBindings.Add(RuntimeTelegramTaskBinding.Create(
                chatId,
                messageThreadId,
                taskId,
                detailMessageId,
                DateTimeOffset.UtcNow));

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RuntimeTelegramTaskBinding>> ListTaskBindingsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RuntimeTelegramTaskBinding>>(TaskBindings);

        public Task ForgetTaskBindingAsync(
            long chatId,
            int? messageThreadId,
            TaskId taskId,
            CancellationToken cancellationToken)
        {
            TaskBindings.RemoveAll(binding =>
                binding.ChatId == chatId
                && binding.MessageThreadId == messageThreadId
                && binding.TaskId == taskId);

            return Task.CompletedTask;
        }

        public Task<Aeges.Application.ApplicationResult<TelegramTaskReviewSnapshot>> GetTaskReviewAsync(
            TaskId taskId,
            CancellationToken cancellationToken)
        {
            var result = WatchedTask is not null && WatchedTask.Id == taskId
                ? Aeges.Application.ApplicationResult<TelegramTaskReviewSnapshot>.Success(
                    new TelegramTaskReviewSnapshot(WatchedTask, [], [], "/runtime/artifacts", [], RuntimeEvents, LatestRunnerResponse: null))
                : Aeges.Application.ApplicationResult<TelegramTaskReviewSnapshot>.Failure(
                    "task_not_found",
                    $"Task '{taskId}' was not found.");

            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<RuntimeEvent>> ListTaskRuntimeEventsAsync(
            TaskId taskId,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RuntimeEvent>>(RuntimeEvents.Take(limit).ToArray());

        public Task<Aeges.Application.ApplicationResult<RuntimeTask>> CancelTaskAsync(
            TaskId taskId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<RuntimeTask>.Failure(
                "task_not_found",
                $"Task '{taskId}' was not found."));

        public Task<Aeges.Application.ApplicationResult<RuntimeTask>> CompleteTaskAsync(
            TaskId taskId,
            CancellationToken cancellationToken)
        {
            if (WatchedTask is null || WatchedTask.Id != taskId)
            {
                return Task.FromResult(Aeges.Application.ApplicationResult<RuntimeTask>.Failure(
                    "task_not_found",
                    $"Task '{taskId}' was not found."));
            }

            WatchedTask.Complete(DateTimeOffset.UtcNow);

            return Task.FromResult(Aeges.Application.ApplicationResult<RuntimeTask>.Success(WatchedTask));
        }

        public Task<Aeges.Application.ApplicationResult<RuntimeTask>> ContinueTaskAsync(
            TaskId taskId,
            string feedback,
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<RuntimeTask>.Failure(
                "task_not_found",
                $"Task '{taskId}' was not found."));

        public Task<Aeges.Application.ApplicationResult<ApprovalRequest>> GetApprovalAsync(
            ApprovalId approvalId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<ApprovalRequest>.Failure(
                "approval_not_found",
                $"Approval request '{approvalId}' was not found."));

        public Task<Aeges.Application.ApplicationResult<ApprovalRequest>> ApproveApprovalAsync(
            ApprovalId approvalId,
            string resolvedBy,
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<ApprovalRequest>.Failure(
                "approval_not_found",
                $"Approval request '{approvalId}' was not found."));

        public Task<Aeges.Application.ApplicationResult<ApprovalRequest>> RejectApprovalAsync(
            ApprovalId approvalId,
            string resolvedBy,
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<ApprovalRequest>.Failure(
                "approval_not_found",
                $"Approval request '{approvalId}' was not found."));
    }
}
