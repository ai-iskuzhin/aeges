using Aeges.Core;
using Aeges.Runners;

namespace Aeges.Runners.Tests;

public sealed class AegesRunnerContractTests
{
    [Fact]
    public async Task Runner_contract_is_cancellation_aware()
    {
        var runner = new CapturingRunner();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await runner.RunAsync(CreateRequest(), cancellation.Token);

        Assert.Equal(new RunnerId("test-runner"), runner.Id);
        Assert.Equal(RunnerStatus.Cancelled, result.Status);
        Assert.True(runner.SawCancellation);
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

    private sealed class CapturingRunner : IAegesRunner
    {
        public RunnerId Id { get; } = new("test-runner");

        public bool SawCancellation { get; private set; }

        public Task<RunnerResult> RunAsync(RunnerRequest request, CancellationToken cancellationToken)
        {
            SawCancellation = cancellationToken.IsCancellationRequested;

            return Task.FromResult(
                cancellationToken.IsCancellationRequested
                    ? RunnerResult.Cancelled("Cancelled before execution.")
                    : RunnerResult.Succeeded());
        }
    }
}
