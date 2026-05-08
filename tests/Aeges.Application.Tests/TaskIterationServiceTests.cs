using Aeges.Application.Iterations;
using Aeges.Application.Machines;
using Aeges.Application.Projects;
using Aeges.Application.Tasks;
using Aeges.Core;

namespace Aeges.Application.Tests;

public sealed class TaskIterationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 08, 13, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task CreateNextAsync_creates_iteration_and_advances_task_counter()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAsync(unitOfWork, clock);
        var service = new TaskIterationService(unitOfWork, clock);

        var result = await service.CreateNextAsync(
            new CreateTaskIterationRequest(
                new TaskId("task-001"),
                new RunnerId("codex"),
                new IterationId("iteration-001")),
            CancellationToken.None);

        var task = await unitOfWork.Tasks.GetByIdAsync(new TaskId("task-001"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(new IterationId("iteration-001"), result.Value.Id);
        Assert.Equal(1, result.Value.IterationNumber);
        Assert.Equal(new RunnerId("codex"), result.Value.RunnerId);
        Assert.Equal(Now, result.Value.CreatedAt);
        Assert.Equal(1, task?.CurrentIteration);
    }

    [Fact]
    public async Task CreateNextAsync_creates_iterations_in_sequence()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAsync(unitOfWork, clock);
        var service = new TaskIterationService(unitOfWork, clock);

        await service.CreateNextAsync(
            new CreateTaskIterationRequest(new TaskId("task-001"), new RunnerId("codex"), new IterationId("iteration-001")),
            CancellationToken.None);
        var result = await service.CreateNextAsync(
            new CreateTaskIterationRequest(new TaskId("task-001"), new RunnerId("codex"), new IterationId("iteration-002")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value?.IterationNumber);
    }

    [Fact]
    public async Task CreateNextAsync_requires_existing_task()
    {
        var service = new TaskIterationService(new InMemoryUnitOfWork(), new FixedClock(Now));

        var result = await service.CreateNextAsync(
            new CreateTaskIterationRequest(new TaskId("missing"), new RunnerId("codex")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("task_not_found", result.Error?.Code);
    }

    [Fact]
    public async Task CreateNextAsync_rejects_terminal_task()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAsync(unitOfWork, clock);
        var taskService = new TaskService(unitOfWork, clock);
        await taskService.StartPlanningAsync(new TaskId("task-001"), CancellationToken.None);
        await taskService.StartRunningAsync(new TaskId("task-001"), CancellationToken.None);
        await taskService.StartReviewAsync(new TaskId("task-001"), CancellationToken.None);
        await taskService.CompleteAsync(new TaskId("task-001"), CancellationToken.None);
        var service = new TaskIterationService(unitOfWork, clock);

        var result = await service.CreateNextAsync(
            new CreateTaskIterationRequest(new TaskId("task-001"), new RunnerId("codex")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("task_terminal", result.Error?.Code);
    }

    [Fact]
    public async Task CreateNextAsync_rejects_iteration_limit()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAsync(unitOfWork, clock, maxIterations: 1);
        var service = new TaskIterationService(unitOfWork, clock);
        await service.CreateNextAsync(
            new CreateTaskIterationRequest(new TaskId("task-001"), new RunnerId("codex"), new IterationId("iteration-001")),
            CancellationToken.None);

        var result = await service.CreateNextAsync(
            new CreateTaskIterationRequest(new TaskId("task-001"), new RunnerId("codex"), new IterationId("iteration-002")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("iteration_limit_reached", result.Error?.Code);
    }

    [Fact]
    public async Task List_and_get_return_created_iterations()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAsync(unitOfWork, clock);
        var service = new TaskIterationService(unitOfWork, clock);
        await service.CreateNextAsync(
            new CreateTaskIterationRequest(new TaskId("task-001"), new RunnerId("codex"), new IterationId("iteration-001")),
            CancellationToken.None);

        var byTask = await service.ListByTaskAsync(new TaskId("task-001"), CancellationToken.None);
        var loaded = await service.GetAsync(new IterationId("iteration-001"), CancellationToken.None);

        Assert.Single(byTask);
        Assert.True(loaded.IsSuccess);
        Assert.Equal(new IterationId("iteration-001"), loaded.Value?.Id);
    }

    private static async Task SeedTaskAsync(
        InMemoryUnitOfWork unitOfWork,
        FixedClock clock,
        int maxIterations = 3)
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
                "Create iteration",
                "Create the next bounded execution iteration.",
                MaxIterations: maxIterations,
                TaskId: new TaskId("task-001")),
            CancellationToken.None);
    }
}
