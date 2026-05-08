using Aeges.Application.Configuration;
using Aeges.Core;
using Aeges.Telegram;

namespace Aeges.Telegram.Tests;

public sealed class TelegramLongPollingServiceTests
{
    [Fact]
    public async Task PollOnceAsync_answers_callback_and_sends_handler_response()
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
                    CallbackQueryId: "callback-001"),
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
        Assert.Single(gateway.SentResponses);
        Assert.Equal("No queued tasks.", gateway.SentResponses[0].Response.Text);
        Assert.Equal(25, gateway.LastLimit);
        Assert.Equal(5, gateway.LastTimeoutSeconds);
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

    private static TelegramLongPollingService CreateService(FakeTelegramBotGateway gateway)
    {
        var handler = new TelegramInteractionHandler(
            new FakeTelegramApplicationFacade(),
            new AegesTelegramConfiguration());

        return new TelegramLongPollingService(gateway, handler);
    }

    private sealed class FakeTelegramBotGateway : ITelegramBotGateway
    {
        public IReadOnlyList<TelegramBotUpdate> Updates { get; init; } = [];

        public List<string> AnsweredCallbackQueryIds { get; } = [];

        public List<(long ChatId, TelegramResponse Response)> SentResponses { get; } = [];

        public int? LastLimit { get; private set; }

        public int? LastTimeoutSeconds { get; private set; }

        public Task<IReadOnlyList<TelegramBotUpdate>> GetUpdatesAsync(
            int? offset,
            int limit,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            LastLimit = limit;
            LastTimeoutSeconds = timeoutSeconds;

            return Task.FromResult(Updates);
        }

        public Task SendResponseAsync(
            long chatId,
            TelegramResponse response,
            CancellationToken cancellationToken)
        {
            SentResponses.Add((chatId, response));
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
        public Task<IReadOnlyList<RuntimeProject>> ListProjectsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RuntimeProject>>([]);

        public Task<IReadOnlyList<RuntimeMachine>> ListMachinesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RuntimeMachine>>([]);

        public Task<IReadOnlyList<RuntimeTask>> ListQueuedTasksAsync(
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RuntimeTask>>([]);

        public Task<IReadOnlyList<ApprovalRequest>> ListPendingApprovalsAsync(
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ApprovalRequest>>([]);

        public Task<Aeges.Application.ApplicationResult<RuntimeTask>> GetTaskAsync(
            TaskId taskId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Aeges.Application.ApplicationResult<RuntimeTask>.Failure(
                "task_not_found",
                $"Task '{taskId}' was not found."));

        public Task<Aeges.Application.ApplicationResult<RuntimeTask>> CancelTaskAsync(
            TaskId taskId,
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
