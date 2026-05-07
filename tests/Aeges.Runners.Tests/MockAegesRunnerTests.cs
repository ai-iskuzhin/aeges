using Aeges.Core;
using Aeges.Runners;

namespace Aeges.Runners.Tests;

public sealed class MockAegesRunnerTests
{
    [Fact]
    public async Task RunAsync_succeeds_and_records_request()
    {
        var runner = new MockAegesRunner();
        var request = CreateRequest();

        var result = await runner.RunAsync(request, CancellationToken.None);

        Assert.Equal(new RunnerId("mock"), runner.Id);
        Assert.Same(request, runner.LastRequest);
        Assert.Equal(1, runner.InvocationCount);
        Assert.Equal(RunnerStatus.Succeeded, result.Status);
        Assert.Equal("/tmp/aeges/artifacts/task-001/result.md", result.ResultArtifactPath);
        Assert.Contains("/tmp/aeges/artifacts/task-001/stdout.log", result.ProducedArtifactPaths);
    }

    [Theory]
    [InlineData(MockRunnerBehavior.Fail, RunnerStatus.Failed)]
    [InlineData(MockRunnerBehavior.TimeOut, RunnerStatus.TimedOut)]
    [InlineData(MockRunnerBehavior.RequireApproval, RunnerStatus.ApprovalRequired)]
    public async Task RunAsync_reports_configured_non_success_behavior(
        MockRunnerBehavior behavior,
        RunnerStatus expectedStatus)
    {
        var runner = new MockAegesRunner(
            new MockRunnerOptions(
                new RunnerId("mock-custom"),
                behavior,
                ErrorSummary: "Configured result."));

        var result = await runner.RunAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(new RunnerId("mock-custom"), runner.Id);
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal("Configured result.", result.ErrorSummary);
    }

    [Fact]
    public async Task RunAsync_returns_cancelled_when_token_is_cancelled_before_execution()
    {
        var runner = new MockAegesRunner();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await runner.RunAsync(CreateRequest(), cancellation.Token);

        Assert.Equal(RunnerStatus.Cancelled, result.Status);
        Assert.Equal("Mock runner was cancelled before execution.", result.ErrorSummary);
    }

    [Fact]
    public async Task RunAsync_returns_cancelled_when_token_is_cancelled_during_delay()
    {
        var runner = new MockAegesRunner(new MockRunnerOptions(ExecutionDelay: TimeSpan.FromMinutes(5)));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));

        var result = await runner.RunAsync(CreateRequest(), cancellation.Token);

        Assert.Equal(RunnerStatus.Cancelled, result.Status);
        Assert.Equal("Mock runner was cancelled during execution.", result.ErrorSummary);
    }

    [Fact]
    public void Create_rejects_invalid_options()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MockAegesRunner(new MockRunnerOptions(ExecutionDelay: TimeSpan.FromSeconds(-1))));
        Assert.Throws<ArgumentException>(
            () => new MockAegesRunner(new MockRunnerOptions(ErrorSummary: " ")));
    }

    private static RunnerRequest CreateRequest() =>
        new(
            new TaskId("task-001"),
            new IterationId("iteration-001"),
            new ProjectId("project-001"),
            "/work/aeges",
            "/tmp/aeges/worktrees/project-001/task-001",
            "/tmp/aeges/artifacts/task-001/prompt.md",
            "/tmp/aeges/artifacts/task-001",
            TimeSpan.FromMinutes(30));
}
