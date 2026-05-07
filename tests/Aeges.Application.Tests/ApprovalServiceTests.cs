using Aeges.Application.Approvals;
using Aeges.Application.Machines;
using Aeges.Application.Projects;
using Aeges.Application.Tasks;
using Aeges.Core;

namespace Aeges.Application.Tests;

public sealed class ApprovalServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 07, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task RequestAsync_creates_approval_and_moves_task_to_waiting_approval()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedPlanningTaskAsync(unitOfWork, clock);
        var service = new ApprovalService(unitOfWork, clock);
        clock.Now = Now.AddMinutes(2);

        var result = await service.RequestAsync(
            new RequestApprovalRequest(
                new TaskId("task-001"),
                IterationId: null,
                "Dependency change requires approval.",
                "Add EF Core SQLite package.",
                new ApprovalId("approval-001")),
            CancellationToken.None);
        var task = await unitOfWork.Tasks.GetByIdAsync(new TaskId("task-001"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(new ApprovalId("approval-001"), result.Value.Id);
        Assert.Equal(ApprovalStatus.Pending, result.Value.Status);
        Assert.Equal(Now.AddMinutes(2), result.Value.CreatedAt);
        Assert.NotNull(task);
        Assert.Equal(RuntimeTaskStatus.WaitingApproval, task.Status);
        Assert.Equal(Now.AddMinutes(2), task.UpdatedAt);
    }

    [Fact]
    public async Task RequestAsync_requires_existing_task()
    {
        var service = new ApprovalService(new InMemoryUnitOfWork(), new FixedClock(Now));

        var result = await service.RequestAsync(
            new RequestApprovalRequest(new TaskId("missing"), null, "Needs approval.", "Do work."),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("task_not_found", result.Error?.Code);
    }

    [Fact]
    public async Task RequestAsync_requires_matching_iteration_when_iteration_is_supplied()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedPlanningTaskAsync(unitOfWork, clock);
        var service = new ApprovalService(unitOfWork, clock);

        var result = await service.RequestAsync(
            new RequestApprovalRequest(new TaskId("task-001"), new IterationId("missing"), "Needs approval.", "Do work."),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("iteration_not_found", result.Error?.Code);
    }

    [Fact]
    public async Task RequestAsync_returns_failure_when_task_cannot_wait_for_approval()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedQueuedTaskAsync(unitOfWork, clock);
        var service = new ApprovalService(unitOfWork, clock);
        var saveCount = unitOfWork.SaveChangesCount;

        var result = await service.RequestAsync(
            new RequestApprovalRequest(new TaskId("task-001"), null, "Needs approval.", "Do work."),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("approval_request_not_allowed", result.Error?.Code);
        Assert.Equal(saveCount, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task List_methods_return_created_approvals()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedPlanningTaskAsync(unitOfWork, clock);
        var service = new ApprovalService(unitOfWork, clock);
        await service.RequestAsync(
            new RequestApprovalRequest(
                new TaskId("task-001"),
                null,
                "Dependency change requires approval.",
                "Add EF Core SQLite package.",
                new ApprovalId("approval-001")),
            CancellationToken.None);

        var byTask = await service.ListByTaskAsync(new TaskId("task-001"), CancellationToken.None);
        var pending = await service.ListPendingAsync(limit: 1, CancellationToken.None);
        var loaded = await service.GetAsync(new ApprovalId("approval-001"), CancellationToken.None);

        Assert.Single(byTask);
        Assert.Single(pending);
        Assert.True(loaded.IsSuccess);
        Assert.Equal(new ApprovalId("approval-001"), loaded.Value?.Id);
    }

    [Fact]
    public async Task ApproveAsync_resolves_approval_and_resumes_task()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedPendingApprovalAsync(unitOfWork, clock);
        var service = new ApprovalService(unitOfWork, clock);
        clock.Now = Now.AddMinutes(3);

        var result = await service.ApproveAsync(
            new ResolveApprovalRequest(new ApprovalId("approval-001"), "operator"),
            CancellationToken.None);
        var task = await unitOfWork.Tasks.GetByIdAsync(new TaskId("task-001"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(ApprovalStatus.Approved, result.Value.Status);
        Assert.Equal("operator", result.Value.ResolvedBy);
        Assert.Equal(Now.AddMinutes(3), result.Value.ResolvedAt);
        Assert.NotNull(task);
        Assert.Equal(RuntimeTaskStatus.Running, task.Status);
    }

    [Fact]
    public async Task RejectAsync_resolves_approval_and_fails_task()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedPendingApprovalAsync(unitOfWork, clock);
        var service = new ApprovalService(unitOfWork, clock);
        clock.Now = Now.AddMinutes(3);

        var result = await service.RejectAsync(
            new ResolveApprovalRequest(new ApprovalId("approval-001"), "operator"),
            CancellationToken.None);
        var task = await unitOfWork.Tasks.GetByIdAsync(new TaskId("task-001"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(ApprovalStatus.Rejected, result.Value.Status);
        Assert.NotNull(task);
        Assert.Equal(RuntimeTaskStatus.Failed, task.Status);
        Assert.Equal("Approval 'approval-001' rejected: Dependency change requires approval.", task.FailureReason);
    }

    [Fact]
    public async Task CancelAsync_resolves_approval_and_cancels_task()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedPendingApprovalAsync(unitOfWork, clock);
        var service = new ApprovalService(unitOfWork, clock);
        clock.Now = Now.AddMinutes(3);

        var result = await service.CancelAsync(
            new ResolveApprovalRequest(new ApprovalId("approval-001"), "runtime"),
            CancellationToken.None);
        var task = await unitOfWork.Tasks.GetByIdAsync(new TaskId("task-001"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(ApprovalStatus.Cancelled, result.Value.Status);
        Assert.NotNull(task);
        Assert.Equal(RuntimeTaskStatus.Cancelled, task.Status);
        Assert.Equal(Now.AddMinutes(3), task.CancelledAt);
    }

    [Fact]
    public async Task Resolve_methods_return_failure_when_approval_is_missing()
    {
        var service = new ApprovalService(new InMemoryUnitOfWork(), new FixedClock(Now));

        var result = await service.ApproveAsync(
            new ResolveApprovalRequest(new ApprovalId("missing"), "operator"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("approval_not_found", result.Error?.Code);
    }

    [Fact]
    public async Task Resolve_methods_return_failure_when_approval_is_already_resolved()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedPendingApprovalAsync(unitOfWork, clock);
        var service = new ApprovalService(unitOfWork, clock);
        await service.ApproveAsync(new ResolveApprovalRequest(new ApprovalId("approval-001"), "operator"), CancellationToken.None);
        var saveCount = unitOfWork.SaveChangesCount;

        var result = await service.RejectAsync(
            new ResolveApprovalRequest(new ApprovalId("approval-001"), "operator"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("approval_already_resolved", result.Error?.Code);
        Assert.Equal(saveCount, unitOfWork.SaveChangesCount);
    }

    private static async Task SeedPendingApprovalAsync(InMemoryUnitOfWork unitOfWork, FixedClock clock)
    {
        await SeedPlanningTaskAsync(unitOfWork, clock);
        await new ApprovalService(unitOfWork, clock).RequestAsync(
            new RequestApprovalRequest(
                new TaskId("task-001"),
                null,
                "Dependency change requires approval.",
                "Add EF Core SQLite package.",
                new ApprovalId("approval-001")),
            CancellationToken.None);
    }

    private static async Task SeedPlanningTaskAsync(InMemoryUnitOfWork unitOfWork, FixedClock clock)
    {
        await SeedQueuedTaskAsync(unitOfWork, clock);
        await new TaskService(unitOfWork, clock).StartPlanningAsync(new TaskId("task-001"), CancellationToken.None);
    }

    private static async Task SeedQueuedTaskAsync(InMemoryUnitOfWork unitOfWork, FixedClock clock)
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
                "Build CLI",
                "Create commands.",
                TaskId: new TaskId("task-001")),
            CancellationToken.None);
    }
}
