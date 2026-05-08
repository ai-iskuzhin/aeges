using Aeges.Application.Machines;
using Aeges.Application.Projects;
using Aeges.Application.RunnerExecutions;
using Aeges.Application.Tasks;
using Aeges.Core;

namespace Aeges.Application.Tests;

public sealed class RunnerExecutionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 08, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task StartAsync_records_runner_execution()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAndIterationAsync(unitOfWork, clock);
        var service = new RunnerExecutionService(unitOfWork, clock);

        var result = await service.StartAsync(
            new StartRunnerExecutionRequest(
                new TaskId("task-001"),
                new IterationId("iteration-001"),
                new RunnerId("codex"),
                "codex exec --json prompt.md",
                "/work/aeges",
                new RunnerExecutionId("runner-execution-001")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(new RunnerExecutionId("runner-execution-001"), result.Value.Id);
        Assert.Equal(new TaskId("task-001"), result.Value.TaskId);
        Assert.Equal(new IterationId("iteration-001"), result.Value.IterationId);
        Assert.Equal(new RunnerId("codex"), result.Value.RunnerId);
        Assert.Equal(Now, result.Value.StartedAt);

        var stored = await unitOfWork.RunnerExecutions.GetByIdAsync(
            new RunnerExecutionId("runner-execution-001"),
            CancellationToken.None);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task StartAsync_requires_existing_task()
    {
        var service = new RunnerExecutionService(new InMemoryUnitOfWork(), new FixedClock(Now));

        var result = await service.StartAsync(
            new StartRunnerExecutionRequest(
                new TaskId("missing"),
                new IterationId("iteration-001"),
                new RunnerId("codex"),
                "codex exec --json prompt.md",
                "/work/aeges"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("task_not_found", result.Error?.Code);
    }

    [Fact]
    public async Task StartAsync_requires_matching_iteration()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAsync(unitOfWork, clock);
        var service = new RunnerExecutionService(unitOfWork, clock);

        var result = await service.StartAsync(
            new StartRunnerExecutionRequest(
                new TaskId("task-001"),
                new IterationId("missing"),
                new RunnerId("codex"),
                "codex exec --json prompt.md",
                "/work/aeges"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("iteration_not_found", result.Error?.Code);
    }

    [Fact]
    public async Task StartAsync_requires_iteration_runner_match()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAndIterationAsync(unitOfWork, clock);
        var service = new RunnerExecutionService(unitOfWork, clock);

        var result = await service.StartAsync(
            new StartRunnerExecutionRequest(
                new TaskId("task-001"),
                new IterationId("iteration-001"),
                new RunnerId("mock"),
                "mock-runner",
                "/work/aeges"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("runner_mismatch", result.Error?.Code);
    }

    [Fact]
    public async Task RecordExitAsync_completes_runner_execution()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAndIterationAsync(unitOfWork, clock);
        var service = new RunnerExecutionService(unitOfWork, clock);
        await StartExecutionAsync(service);
        clock.Now = Now.AddMinutes(3);

        var result = await service.RecordExitAsync(
            new RunnerExecutionId("runner-execution-001"),
            1,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value?.ExitCode);
        Assert.Equal(Now.AddMinutes(3), result.Value?.CompletedAt);
    }

    [Fact]
    public async Task RecordTimeoutAsync_marks_runner_execution_timed_out()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAndIterationAsync(unitOfWork, clock);
        var service = new RunnerExecutionService(unitOfWork, clock);
        await StartExecutionAsync(service);
        clock.Now = Now.AddMinutes(30);

        var result = await service.RecordTimeoutAsync(
            new RunnerExecutionId("runner-execution-001"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value?.TimedOut);
        Assert.Equal(Now.AddMinutes(30), result.Value?.CompletedAt);
    }

    [Fact]
    public async Task Completion_methods_return_failure_when_execution_already_completed()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAndIterationAsync(unitOfWork, clock);
        var service = new RunnerExecutionService(unitOfWork, clock);
        await StartExecutionAsync(service);
        await service.RecordExitAsync(new RunnerExecutionId("runner-execution-001"), 0, CancellationToken.None);

        var result = await service.RecordCancellationAsync(
            new RunnerExecutionId("runner-execution-001"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("runner_execution_domain_rule_violation", result.Error?.Code);
    }

    [Fact]
    public async Task List_methods_return_runner_executions()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAndIterationAsync(unitOfWork, clock);
        var service = new RunnerExecutionService(unitOfWork, clock);
        await StartExecutionAsync(service);

        var byTask = await service.ListByTaskAsync(new TaskId("task-001"), CancellationToken.None);
        var byIteration = await service.ListByIterationAsync(new IterationId("iteration-001"), CancellationToken.None);
        var loaded = await service.GetAsync(new RunnerExecutionId("runner-execution-001"), CancellationToken.None);

        Assert.Single(byTask);
        Assert.Single(byIteration);
        Assert.True(loaded.IsSuccess);
        Assert.Equal(new RunnerExecutionId("runner-execution-001"), loaded.Value?.Id);
    }

    private static async Task StartExecutionAsync(RunnerExecutionService service)
    {
        await service.StartAsync(
            new StartRunnerExecutionRequest(
                new TaskId("task-001"),
                new IterationId("iteration-001"),
                new RunnerId("codex"),
                "codex exec --json prompt.md",
                "/work/aeges",
                new RunnerExecutionId("runner-execution-001")),
            CancellationToken.None);
    }

    private static async Task SeedTaskAndIterationAsync(InMemoryUnitOfWork unitOfWork, FixedClock clock)
    {
        await SeedTaskAsync(unitOfWork, clock);
        await unitOfWork.Iterations.AddAsync(
            TaskIteration.Create(
                new IterationId("iteration-001"),
                new TaskId("task-001"),
                1,
                new RunnerId("codex"),
                Now),
            CancellationToken.None);
    }

    private static async Task SeedTaskAsync(InMemoryUnitOfWork unitOfWork, FixedClock clock)
    {
        await new ProjectService(unitOfWork, clock).RegisterAsync(
            new RegisterProjectRequest("Aeges", "/work/aeges", new ProjectId("project-001")),
            CancellationToken.None);
        await new MachineService(unitOfWork, clock).RegisterAsync(
            new RegisterMachineRequest("home-laptop", "macOS arm64", new MachineId("machine-001")),
            CancellationToken.None);
        await new TaskService(unitOfWork, clock).CreateAsync(
            new CreateTaskRequest(
                new ProjectId("project-001"),
                new MachineId("machine-001"),
                "Build runner execution tracking",
                "Record every worker process launch.",
                TaskId: new TaskId("task-001")),
            CancellationToken.None);
    }
}
