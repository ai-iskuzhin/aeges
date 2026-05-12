using Aeges.Application.Configuration;
using Aeges.Core;
using Aeges.Telegram;

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
        Assert.Contains("completed tasks:", gateway.EditedResponses[1].Response.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_continues_after_transient_polling_failure()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
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
        var service = CreateService(gateway, transientErrorDelay: TimeSpan.Zero);

        await service.RunAsync(new TelegramLongPollingOptions(), cancellation.Token);

        Assert.Equal(2, gateway.GetUpdatesCallCount);
        Assert.Single(gateway.SentResponses);
        Assert.Single(gateway.EditedResponses);
        Assert.Contains("Working on your message", gateway.SentResponses[0].Response.Text, StringComparison.Ordinal);
        Assert.Equal("talk_unavailable: Talk is not available in this test facade.", gateway.EditedResponses[0].Response.Text);
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
        TimeSpan? transientErrorDelay = null)
    {
        var handler = new TelegramInteractionHandler(
            facade ?? new FakeTelegramApplicationFacade(),
            new AegesTelegramConfiguration());

        return new TelegramLongPollingService(gateway, handler, transientErrorDelay);
    }

    private sealed class FakeTelegramBotGateway : ITelegramBotGateway
    {
        public IReadOnlyList<TelegramBotUpdate> Updates { get; set; } = [];

        public bool ThrowTransientPollingFailureOnce { get; set; }

        public int GetUpdatesCallCount { get; private set; }

        public event EventHandler? BeforeGetUpdates;

        public event EventHandler? ResponseSent;

        public event EventHandler? ResponseEdited;

        public List<string> AnsweredCallbackQueryIds { get; } = [];

        public List<(long ChatId, TelegramResponse Response)> SentResponses { get; } = [];

        public List<(long ChatId, int MessageId, TelegramResponse Response)> EditedResponses { get; } = [];

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
            TelegramResponse response,
            CancellationToken cancellationToken)
        {
            SentResponses.Add((chatId, response));
            ResponseSent?.Invoke(this, EventArgs.Empty);
            return Task.FromResult<int?>(SentResponses.Count);
        }

        public Task EditResponseAsync(
            long chatId,
            int messageId,
            TelegramResponse response,
            CancellationToken cancellationToken)
        {
            EditedResponses.Add((chatId, messageId, response));
            ResponseEdited?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task AnswerCallbackQueryAsync(
            string callbackQueryId,
            CancellationToken cancellationToken)
        {
            AnsweredCallbackQueryIds.Add(callbackQueryId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTelegramApplicationFacade : ITelegramApplicationFacade
    {
        public RuntimeTask? WatchedTask { get; init; }

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
            Task.FromResult(new TelegramRunnerSettings("workspace-write", CodexBypassApprovalsAndSandbox: false));

        public Task<Aeges.Application.ApplicationResult<TelegramRunnerSettings>> SetCodexSandboxModeAsync(
            string sandboxMode,
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<TelegramRunnerSettings>.Success(
                new TelegramRunnerSettings(sandboxMode, CodexBypassApprovalsAndSandbox: false)));

        public Task<Aeges.Application.ApplicationResult<TelegramRunnerSettings>> SetCodexBypassApprovalsAndSandboxAsync(
            bool enabled,
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<TelegramRunnerSettings>.Success(
                new TelegramRunnerSettings("workspace-write", enabled)));

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

        public Task<Aeges.Application.ApplicationResult<TelegramTaskReviewSnapshot>> GetTaskReviewAsync(
            TaskId taskId,
            CancellationToken cancellationToken)
        {
            var result = WatchedTask is not null && WatchedTask.Id == taskId
                ? Aeges.Application.ApplicationResult<TelegramTaskReviewSnapshot>.Success(
                    new TelegramTaskReviewSnapshot(WatchedTask, [], [], "/runtime/artifacts", [], LatestRunnerResponse: null))
                : Aeges.Application.ApplicationResult<TelegramTaskReviewSnapshot>.Failure(
                    "task_not_found",
                    $"Task '{taskId}' was not found.");

            return Task.FromResult(result);
        }

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
