using Aeges.Application.Locks;
using Aeges.Application.Machines;
using Aeges.Application.Projects;
using Aeges.Application.Tasks;
using Aeges.Core;

namespace Aeges.Application.Tests;

public sealed class LockServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 07, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task AcquireAsync_adds_lock_when_no_conflict_exists()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAsync(unitOfWork, clock, new TaskId("task-001"));
        var service = new LockService(unitOfWork, clock);

        var result = await service.AcquireAsync(
            new AcquireLockRequest(
                new TaskId("task-001"),
                new ProjectId("project-001"),
                "src/Auth/*",
                new LockId("lock-001")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(new LockId("lock-001"), result.Value.Id);
        Assert.Equal("src/Auth/*", result.Value.PathPattern);
        Assert.True(result.Value.IsActive);
        Assert.Single(await service.ListActiveByProjectAsync(new ProjectId("project-001"), CancellationToken.None));
    }

    [Fact]
    public async Task AcquireAsync_returns_conflict_when_another_task_holds_overlapping_lock()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAsync(unitOfWork, clock, new TaskId("task-001"));
        await SeedTaskAsync(unitOfWork, clock, new TaskId("task-002"));
        var service = new LockService(unitOfWork, clock);
        await service.AcquireAsync(
            new AcquireLockRequest(new TaskId("task-001"), new ProjectId("project-001"), "src/Auth/*", new LockId("lock-001")),
            CancellationToken.None);
        var saveCount = unitOfWork.SaveChangesCount;

        var result = await service.AcquireAsync(
            new AcquireLockRequest(new TaskId("task-002"), new ProjectId("project-001"), "src/Auth/Login.cs", new LockId("lock-002")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("lock_conflict", result.Error?.Code);
        Assert.Equal(saveCount, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task AcquireAsync_allows_overlapping_lock_for_same_task()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAsync(unitOfWork, clock, new TaskId("task-001"));
        var service = new LockService(unitOfWork, clock);
        await service.AcquireAsync(
            new AcquireLockRequest(new TaskId("task-001"), new ProjectId("project-001"), "src/Auth/*", new LockId("lock-001")),
            CancellationToken.None);

        var result = await service.AcquireAsync(
            new AcquireLockRequest(new TaskId("task-001"), new ProjectId("project-001"), "src/Auth/Login.cs", new LockId("lock-002")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, (await service.ListActiveByTaskAsync(new TaskId("task-001"), CancellationToken.None)).Count);
    }

    [Fact]
    public async Task AcquireAsync_requires_existing_task()
    {
        var service = new LockService(new InMemoryUnitOfWork(), new FixedClock(Now));

        var result = await service.AcquireAsync(
            new AcquireLockRequest(new TaskId("missing"), new ProjectId("project-001"), "src/Auth/*"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("task_not_found", result.Error?.Code);
    }

    [Fact]
    public async Task AcquireAsync_requires_matching_project()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAsync(unitOfWork, clock, new TaskId("task-001"));
        var service = new LockService(unitOfWork, clock);

        var result = await service.AcquireAsync(
            new AcquireLockRequest(new TaskId("task-001"), new ProjectId("project-002"), "src/Auth/*"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("project_mismatch", result.Error?.Code);
    }

    [Fact]
    public async Task AcquireAsync_returns_failure_for_invalid_path_pattern()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAsync(unitOfWork, clock, new TaskId("task-001"));
        var service = new LockService(unitOfWork, clock);
        var saveCount = unitOfWork.SaveChangesCount;

        var result = await service.AcquireAsync(
            new AcquireLockRequest(new TaskId("task-001"), new ProjectId("project-001"), "../outside"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_lock_path", result.Error?.Code);
        Assert.Equal(saveCount, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ReleaseAsync_releases_active_lock()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAsync(unitOfWork, clock, new TaskId("task-001"));
        var service = new LockService(unitOfWork, clock);
        await service.AcquireAsync(
            new AcquireLockRequest(new TaskId("task-001"), new ProjectId("project-001"), "src/Auth/*", new LockId("lock-001")),
            CancellationToken.None);
        clock.Now = Now.AddMinutes(1);

        var result = await service.ReleaseAsync(new LockId("lock-001"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.False(result.Value.IsActive);
        Assert.Equal(Now.AddMinutes(1), result.Value.ReleasedAt);
        Assert.Empty(await service.ListActiveByProjectAsync(new ProjectId("project-001"), CancellationToken.None));
    }

    [Fact]
    public async Task ReleaseAsync_returns_failure_when_lock_is_missing_or_already_released()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAsync(unitOfWork, clock, new TaskId("task-001"));
        var service = new LockService(unitOfWork, clock);
        await service.AcquireAsync(
            new AcquireLockRequest(new TaskId("task-001"), new ProjectId("project-001"), "src/Auth/*", new LockId("lock-001")),
            CancellationToken.None);
        await service.ReleaseAsync(new LockId("lock-001"), CancellationToken.None);

        var missing = await service.ReleaseAsync(new LockId("missing"), CancellationToken.None);
        var alreadyReleased = await service.ReleaseAsync(new LockId("lock-001"), CancellationToken.None);

        Assert.False(missing.IsSuccess);
        Assert.Equal("lock_not_found", missing.Error?.Code);
        Assert.False(alreadyReleased.IsSuccess);
        Assert.Equal("lock_already_released", alreadyReleased.Error?.Code);
    }

    private static async Task SeedTaskAsync(InMemoryUnitOfWork unitOfWork, FixedClock clock, TaskId taskId)
    {
        if (await unitOfWork.Projects.GetByIdAsync(new ProjectId("project-001"), CancellationToken.None) is null)
        {
            await new ProjectService(unitOfWork, clock).RegisterAsync(
                new RegisterProjectRequest("Aeges", "/work/aeges", new ProjectId("project-001")),
                CancellationToken.None);
        }

        if (await unitOfWork.Machines.GetByIdAsync(new MachineId("machine-001"), CancellationToken.None) is null)
        {
            await new MachineService(unitOfWork, clock).RegisterAsync(
                new RegisterMachineRequest("home-laptop", "macOS arm64", new MachineId("machine-001")),
                CancellationToken.None);
        }

        await new TaskService(unitOfWork, clock).CreateAsync(
            new CreateTaskRequest(
                new ProjectId("project-001"),
                new MachineId("machine-001"),
                $"Task {taskId}",
                "Do governed work.",
                TaskId: taskId),
            CancellationToken.None);
    }
}
