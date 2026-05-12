using Aeges.Application;
using Aeges.Application.Transports;
using Aeges.Core;
using Aeges.Storage;
using Aeges.Telegram;

namespace Aeges.Telegram.Tests;

public sealed class TelegramCallbackRegistryTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 11, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task TokenizeAsync_shortens_button_callbacks_and_resolves_original_payload()
    {
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FixedClock(Now);
        var registry = new TelegramCallbackRegistry(new TransportCallbackActionService(unitOfWork, clock), clock);
        var callbackData = $"aeges:project:project-with-a-very-long-id:{new string('x', 120)}";
        var response = new TelegramResponse(
            "Projects",
            new TelegramButtonMarkup([[new TelegramButton("Open", callbackData)]]));

        var tokenized = await registry.TokenizeAsync(1001, response, CancellationToken.None);

        var token = tokenized.Buttons.Rows[0][0].CallbackData;
        Assert.StartsWith("a:", token, StringComparison.Ordinal);
        Assert.True(token.Length < callbackData.Length);

        var resolved = await registry.ResolveAsync(1001, token, CancellationToken.None);

        Assert.Equal(callbackData, resolved);
        Assert.Equal(2, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ResolveAsync_keeps_legacy_callback_data_unchanged()
    {
        var clock = new FixedClock(Now);
        var registry = new TelegramCallbackRegistry(
            new TransportCallbackActionService(new FakeUnitOfWork(), clock),
            clock);

        var resolved = await registry.ResolveAsync(1001, TelegramCallbackData.MainMenu, CancellationToken.None);

        Assert.Equal(TelegramCallbackData.MainMenu, resolved);
    }

    [Fact]
    public async Task ResolveAsync_rejects_tokens_outside_the_original_chat_scope()
    {
        var clock = new FixedClock(Now);
        var registry = new TelegramCallbackRegistry(
            new TransportCallbackActionService(new FakeUnitOfWork(), clock),
            clock);
        var response = new TelegramResponse(
            "Projects",
            new TelegramButtonMarkup([[new TelegramButton("Open", TelegramCallbackData.ListProjects)]]));

        var tokenized = await registry.TokenizeAsync(1001, response, CancellationToken.None);
        var token = tokenized.Buttons.Rows[0][0].CallbackData;

        var resolved = await registry.ResolveAsync(2002, token, CancellationToken.None);

        Assert.Null(resolved);
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset now)
        {
            Now = now;
        }

        public DateTimeOffset Now { get; }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public ITaskRepository Tasks => throw new NotSupportedException();

        public IIterationRepository Iterations => throw new NotSupportedException();

        public IArtifactRepository Artifacts => throw new NotSupportedException();

        public IApprovalRepository Approvals => throw new NotSupportedException();

        public IProjectRepository Projects => throw new NotSupportedException();

        public IProjectGroupRepository ProjectGroups => throw new NotSupportedException();

        public IProjectRootRepository ProjectRoots => throw new NotSupportedException();

        public IMachineRepository Machines => throw new NotSupportedException();

        public ILockRepository Locks => throw new NotSupportedException();

        public IRunnerExecutionRepository RunnerExecutions => throw new NotSupportedException();

        public ITalkSessionRepository TalkSessions => throw new NotSupportedException();

        public ITalkMessageRepository TalkMessages => throw new NotSupportedException();

        public ITransportCallbackActionRepository TransportCallbackActions { get; } =
            new FakeTransportCallbackActionRepository();

        public int SaveChangesCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCount++;
            return Task.FromResult(1);
        }

        public Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken) =>
            operation(cancellationToken);

        public Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken) =>
            operation(cancellationToken);
    }

    private sealed class FakeTransportCallbackActionRepository : ITransportCallbackActionRepository
    {
        private readonly Dictionary<(string Transport, string Token), RuntimeTransportCallbackAction> actions = [];

        public Task AddAsync(RuntimeTransportCallbackAction action, CancellationToken cancellationToken)
        {
            actions[(action.Transport, action.Token)] = action;
            return Task.CompletedTask;
        }

        public Task<RuntimeTransportCallbackAction?> GetAsync(
            string transport,
            string token,
            CancellationToken cancellationToken)
        {
            actions.TryGetValue((transport, token), out var action);
            return Task.FromResult(action);
        }

        public Task UpdateAsync(RuntimeTransportCallbackAction action, CancellationToken cancellationToken)
        {
            actions[(action.Transport, action.Token)] = action;
            return Task.CompletedTask;
        }

        public Task<int> PruneExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            var expiredKeys = actions
                .Where(item => item.Value.IsExpired(now))
                .Select(item => item.Key)
                .ToArray();

            foreach (var key in expiredKeys)
            {
                actions.Remove(key);
            }

            return Task.FromResult(expiredKeys.Length);
        }
    }
}
