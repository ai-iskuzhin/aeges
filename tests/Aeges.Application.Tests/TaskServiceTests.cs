using Aeges.Application.Machines;
using Aeges.Application.Projects;
using Aeges.Application.Tasks;
using Aeges.Core;

namespace Aeges.Application.Tests;

public sealed class TaskServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 07, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_creates_queued_task_when_dependencies_exist()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedProjectAndMachineAsync(unitOfWork, clock);
        var service = new TaskService(unitOfWork, clock);

        var result = await service.CreateAsync(
            new CreateTaskRequest(
                new ProjectId("project-001"),
                new MachineId("machine-001"),
                "Build CLI",
                "Create usable CLI commands.",
                Priority: 5,
                MaxIterations: 2,
                new TaskId("task-001")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(new TaskId("task-001"), result.Value.Id);
        Assert.Equal(RuntimeTaskStatus.Queued, result.Value.Status);
        Assert.Equal(5, result.Value.Priority);
        Assert.Equal(2, result.Value.MaxIterations);
        Assert.Equal(Now, result.Value.CreatedAt);
    }

    [Fact]
    public async Task CreateAsync_returns_failure_when_project_is_missing()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await new MachineService(unitOfWork, clock).RegisterAsync(
            new RegisterMachineRequest("home-laptop", "macOS arm64", new MachineId("machine-001")),
            CancellationToken.None);
        var service = new TaskService(unitOfWork, clock);

        var result = await service.CreateAsync(
            new CreateTaskRequest(new ProjectId("missing"), new MachineId("machine-001"), "Build CLI", "Create commands."),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("project_not_found", result.Error?.Code);
    }

    [Fact]
    public async Task CreateAsync_returns_failure_when_machine_is_missing()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await new ProjectService(unitOfWork, clock).RegisterAsync(
            new RegisterProjectRequest("Aeges", "/work/aeges", new ProjectId("project-001")),
            CancellationToken.None);
        var service = new TaskService(unitOfWork, clock);

        var result = await service.CreateAsync(
            new CreateTaskRequest(new ProjectId("project-001"), new MachineId("missing"), "Build CLI", "Create commands."),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("machine_not_found", result.Error?.Code);
    }

    [Fact]
    public async Task GetAsync_returns_task_or_failure()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedProjectAndMachineAsync(unitOfWork, clock);
        var service = new TaskService(unitOfWork, clock);
        await service.CreateAsync(
            new CreateTaskRequest(new ProjectId("project-001"), new MachineId("machine-001"), "Build CLI", "Create commands.", TaskId: new TaskId("task-001")),
            CancellationToken.None);

        var existing = await service.GetAsync(new TaskId("task-001"), CancellationToken.None);
        var missing = await service.GetAsync(new TaskId("missing"), CancellationToken.None);

        Assert.True(existing.IsSuccess);
        Assert.False(missing.IsSuccess);
        Assert.Equal("task_not_found", missing.Error?.Code);
    }

    [Fact]
    public async Task List_methods_delegate_to_task_repository()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedProjectAndMachineAsync(unitOfWork, clock);
        var service = new TaskService(unitOfWork, clock);
        await service.CreateAsync(
            new CreateTaskRequest(new ProjectId("project-001"), new MachineId("machine-001"), "Low", "Low priority.", TaskId: new TaskId("task-low")),
            CancellationToken.None);
        await service.CreateAsync(
            new CreateTaskRequest(new ProjectId("project-001"), new MachineId("machine-001"), "High", "High priority.", Priority: 10, TaskId: new TaskId("task-high")),
            CancellationToken.None);

        var byProject = await service.ListByProjectAsync(new ProjectId("project-001"), CancellationToken.None);
        var queued = await service.ListByStatusAsync(RuntimeTaskStatus.Queued, 1, CancellationToken.None);

        Assert.Equal(new TaskId("task-high"), byProject[0].Id);
        Assert.Single(queued);
        Assert.Equal(new TaskId("task-high"), queued[0].Id);
    }

    [Fact]
    public async Task Lifecycle_methods_apply_valid_status_transitions()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedProjectAndMachineAsync(unitOfWork, clock);
        var service = new TaskService(unitOfWork, clock);
        await service.CreateAsync(
            new CreateTaskRequest(new ProjectId("project-001"), new MachineId("machine-001"), "Build CLI", "Create commands.", TaskId: new TaskId("task-001")),
            CancellationToken.None);

        clock.Now = Now.AddMinutes(1);
        var planning = await service.StartPlanningAsync(new TaskId("task-001"), CancellationToken.None);
        clock.Now = Now.AddMinutes(2);
        var running = await service.StartRunningAsync(new TaskId("task-001"), CancellationToken.None);
        clock.Now = Now.AddMinutes(3);
        var reviewing = await service.StartReviewAsync(new TaskId("task-001"), CancellationToken.None);
        clock.Now = Now.AddMinutes(4);
        var completed = await service.CompleteAsync(new TaskId("task-001"), CancellationToken.None);

        Assert.True(planning.IsSuccess);
        Assert.True(running.IsSuccess);
        Assert.True(reviewing.IsSuccess);
        Assert.True(completed.IsSuccess);
        Assert.NotNull(completed.Value);
        Assert.Equal(RuntimeTaskStatus.Completed, completed.Value.Status);
        Assert.Equal(Now.AddMinutes(1), completed.Value.StartedAt);
        Assert.Equal(Now.AddMinutes(4), completed.Value.CompletedAt);
        Assert.Equal(Now.AddMinutes(4), completed.Value.UpdatedAt);
    }

    [Fact]
    public async Task WaitForApproval_and_AdvanceIteration_update_task_state()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedProjectAndMachineAsync(unitOfWork, clock);
        var service = new TaskService(unitOfWork, clock);
        await service.CreateAsync(
            new CreateTaskRequest(new ProjectId("project-001"), new MachineId("machine-001"), "Build CLI", "Create commands.", TaskId: new TaskId("task-001")),
            CancellationToken.None);
        await service.StartPlanningAsync(new TaskId("task-001"), CancellationToken.None);

        clock.Now = Now.AddMinutes(2);
        var waiting = await service.WaitForApprovalAsync(new TaskId("task-001"), CancellationToken.None);
        clock.Now = Now.AddMinutes(3);
        var running = await service.StartRunningAsync(new TaskId("task-001"), CancellationToken.None);
        clock.Now = Now.AddMinutes(4);
        var advanced = await service.AdvanceIterationAsync(new TaskId("task-001"), CancellationToken.None);

        Assert.True(waiting.IsSuccess);
        Assert.True(running.IsSuccess);
        Assert.True(advanced.IsSuccess);
        Assert.NotNull(advanced.Value);
        Assert.Equal(RuntimeTaskStatus.Running, advanced.Value.Status);
        Assert.Equal(1, advanced.Value.CurrentIteration);
        Assert.Equal(Now.AddMinutes(4), advanced.Value.UpdatedAt);
    }

    [Fact]
    public async Task CancelAsync_cancels_queued_task()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedProjectAndMachineAsync(unitOfWork, clock);
        var service = new TaskService(unitOfWork, clock);
        await service.CreateAsync(
            new CreateTaskRequest(new ProjectId("project-001"), new MachineId("machine-001"), "Build CLI", "Create commands.", TaskId: new TaskId("task-001")),
            CancellationToken.None);
        clock.Now = Now.AddMinutes(1);

        var result = await service.CancelAsync(new TaskId("task-001"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(RuntimeTaskStatus.Cancelled, result.Value.Status);
        Assert.Equal(Now.AddMinutes(1), result.Value.CancelledAt);
    }

    [Fact]
    public async Task FailAsync_records_failure_reason()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedProjectAndMachineAsync(unitOfWork, clock);
        var service = new TaskService(unitOfWork, clock);
        await service.CreateAsync(
            new CreateTaskRequest(new ProjectId("project-001"), new MachineId("machine-001"), "Build CLI", "Create commands.", TaskId: new TaskId("task-001")),
            CancellationToken.None);
        await service.StartPlanningAsync(new TaskId("task-001"), CancellationToken.None);
        clock.Now = Now.AddMinutes(2);

        var result = await service.FailAsync(new TaskId("task-001"), "Runner unavailable.", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(RuntimeTaskStatus.Failed, result.Value.Status);
        Assert.Equal("Runner unavailable.", result.Value.FailureReason);
        Assert.Equal(Now.AddMinutes(2), result.Value.UpdatedAt);
    }

    [Fact]
    public async Task Transition_methods_return_failure_when_task_is_missing()
    {
        var service = new TaskService(new InMemoryUnitOfWork(), new FixedClock(Now));

        var result = await service.StartPlanningAsync(new TaskId("missing"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("task_not_found", result.Error?.Code);
    }

    [Fact]
    public async Task Transition_methods_return_failure_when_transition_is_invalid()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedProjectAndMachineAsync(unitOfWork, clock);
        var service = new TaskService(unitOfWork, clock);
        await service.CreateAsync(
            new CreateTaskRequest(new ProjectId("project-001"), new MachineId("machine-001"), "Build CLI", "Create commands.", TaskId: new TaskId("task-001")),
            CancellationToken.None);
        var saveCount = unitOfWork.SaveChangesCount;

        var result = await service.CompleteAsync(new TaskId("task-001"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_task_status_transition", result.Error?.Code);
        Assert.Equal(saveCount, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task AdvanceIterationAsync_returns_failure_when_iteration_limit_is_exceeded()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedProjectAndMachineAsync(unitOfWork, clock);
        var service = new TaskService(unitOfWork, clock);
        await service.CreateAsync(
            new CreateTaskRequest(
                new ProjectId("project-001"),
                new MachineId("machine-001"),
                "Build CLI",
                "Create commands.",
                MaxIterations: 1,
                TaskId: new TaskId("task-001")),
            CancellationToken.None);
        await service.AdvanceIterationAsync(new TaskId("task-001"), CancellationToken.None);
        var saveCount = unitOfWork.SaveChangesCount;

        var result = await service.AdvanceIterationAsync(new TaskId("task-001"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("task_domain_rule_violation", result.Error?.Code);
        Assert.Equal(saveCount, unitOfWork.SaveChangesCount);
    }

    private static async Task SeedProjectAndMachineAsync(InMemoryUnitOfWork unitOfWork, FixedClock clock)
    {
        await new ProjectService(unitOfWork, clock).RegisterAsync(
            new RegisterProjectRequest("Aeges", "/work/aeges", new ProjectId("project-001")),
            CancellationToken.None);
        await new MachineService(unitOfWork, clock).RegisterAsync(
            new RegisterMachineRequest("home-laptop", "macOS arm64", new MachineId("machine-001")),
            CancellationToken.None);
    }
}
