using Aeges.Application.Runtime;
using Aeges.Application.Talk;
using Aeges.Core;
using Aeges.Runners;

namespace Aeges.Application.Tests;

public sealed class TalkServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 11, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task SendAsync_creates_session_and_persists_exchange()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var runner = new CapturingTalkRunner("Hello from runner.", "thread-001");
        using var directory = new TemporaryRuntimeDirectory();
        var service = new TalkService(
            unitOfWork,
            new FixedClock(Now),
            runner,
            RuntimeDirectoryLayout.Create(directory.Path),
            TimeSpan.FromSeconds(30));

        var result = await service.SendAsync(
            new SendTalkMessageRequest("telegram:1001", "Can you help me set up Aeges?"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("telegram:1001", result.Value!.Session.Source);
        Assert.Equal("Hello from runner.", result.Value.AssistantMessage.Content);
        Assert.Equal("thread-001", result.Value.Session.ExternalSessionId);
        Assert.Contains("discussion session", runner.LastRequest!.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task SendAsync_reuses_latest_open_source_session()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        using var directory = new TemporaryRuntimeDirectory();
        var service = new TalkService(
            unitOfWork,
            clock,
            new CapturingTalkRunner("First", "thread-001"),
            RuntimeDirectoryLayout.Create(directory.Path),
            TimeSpan.FromSeconds(30));
        var first = await service.SendAsync(new SendTalkMessageRequest("cli", "First"), CancellationToken.None);

        clock.Now = Now.AddMinutes(1);
        var second = await service.SendAsync(new SendTalkMessageRequest("cli", "Second"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.Session.Id, second.Value!.Session.Id);
    }

    [Fact]
    public async Task SendAsync_returns_failure_for_empty_message()
    {
        var service = new TalkService(
            new InMemoryUnitOfWork(),
            new FixedClock(Now),
            new CapturingTalkRunner("Ignored", null),
            RuntimeDirectoryLayout.Create(CreateTemporaryDirectory()),
            TimeSpan.FromSeconds(30));

        var result = await service.SendAsync(new SendTalkMessageRequest("cli", " "), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("empty_talk_message", result.Error?.Code);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "aeges-talk-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);

        return path;
    }

    private sealed class CapturingTalkRunner : ITalkRunner
    {
        private readonly string response;
        private readonly string? externalSessionId;

        public CapturingTalkRunner(string response, string? externalSessionId)
        {
            this.response = response;
            this.externalSessionId = externalSessionId;
        }

        public RunnerId Id { get; } = new("mock-talk");

        public TalkRunnerRequest? LastRequest { get; private set; }

        public Task<TalkRunnerResult> SendAsync(
            TalkRunnerRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;

            return Task.FromResult(TalkRunnerResult.Succeeded(response, externalSessionId: externalSessionId));
        }
    }

    private sealed class TemporaryRuntimeDirectory : IDisposable
    {
        public TemporaryRuntimeDirectory()
        {
            Path = CreateTemporaryDirectory();
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
