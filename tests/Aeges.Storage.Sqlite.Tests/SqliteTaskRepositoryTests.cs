using Aeges.Core;
using Aeges.Storage.Sqlite.Repositories;

namespace Aeges.Storage.Sqlite.Tests;

public sealed class SqliteTaskRepositoryTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 05, 07, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task Add_and_get_round_trips_task()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SeedProjectAndMachineAsync(context);
        var repository = new SqliteTaskRepository(context);
        var task = CreateTask(new TaskId("task-001"));

        await repository.AddAsync(task, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(new TaskId("task-001"), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(task.Id, stored.Id);
        Assert.Equal(task.ProjectId, stored.ProjectId);
        Assert.Equal(task.MachineId, stored.MachineId);
        Assert.Equal(task.Title, stored.Title);
        Assert.Equal(task.Goal, stored.Goal);
        Assert.Equal(task.Status, stored.Status);
        Assert.Equal(task.Priority, stored.Priority);
        Assert.Equal(task.MaxIterations, stored.MaxIterations);
        Assert.Equal(task.CurrentIteration, stored.CurrentIteration);
        Assert.Equal(task.CreatedAt, stored.CreatedAt);
        Assert.Equal(task.UpdatedAt, stored.UpdatedAt);
    }

    [Fact]
    public async Task ListByProject_orders_by_priority_then_created_at()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SeedProjectAndMachineAsync(context);
        var repository = new SqliteTaskRepository(context);

        await repository.AddAsync(CreateTask(new TaskId("task-low-old"), priority: 0, createdAt: CreatedAt), CancellationToken.None);
        await repository.AddAsync(CreateTask(new TaskId("task-high-new"), priority: 10, createdAt: CreatedAt.AddMinutes(2)), CancellationToken.None);
        await repository.AddAsync(CreateTask(new TaskId("task-high-old"), priority: 10, createdAt: CreatedAt.AddMinutes(1)), CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var tasks = await repository.ListByProjectAsync(new ProjectId("project-001"), CancellationToken.None);

        Assert.Collection(
            tasks,
            task => Assert.Equal(new TaskId("task-high-old"), task.Id),
            task => Assert.Equal(new TaskId("task-high-new"), task.Id),
            task => Assert.Equal(new TaskId("task-low-old"), task.Id));
    }

    [Fact]
    public async Task ListByStatus_filters_orders_and_limits_tasks()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SeedProjectAndMachineAsync(context);
        var repository = new SqliteTaskRepository(context);
        var running = CreateTask(new TaskId("task-running"), priority: 100);
        running.StartPlanning(CreatedAt.AddMinutes(1));
        running.StartRunning(CreatedAt.AddMinutes(2));

        await repository.AddAsync(CreateTask(new TaskId("task-low"), priority: 0), CancellationToken.None);
        await repository.AddAsync(CreateTask(new TaskId("task-high"), priority: 10), CancellationToken.None);
        await repository.AddAsync(running, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var queued = await repository.ListByStatusAsync(RuntimeTaskStatus.Queued, limit: 1, CancellationToken.None);

        Assert.Single(queued);
        Assert.Equal(new TaskId("task-high"), queued[0].Id);
    }

    [Fact]
    public async Task Update_persists_task_lifecycle_state()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SeedProjectAndMachineAsync(context);
        var repository = new SqliteTaskRepository(context);
        var task = CreateTask(new TaskId("task-001"));
        await repository.AddAsync(task, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var planningAt = CreatedAt.AddMinutes(1);
        var runningAt = CreatedAt.AddMinutes(2);
        task.StartPlanning(planningAt);
        task.StartRunning(runningAt);
        task.AdvanceIteration(runningAt.AddMinutes(1));
        await repository.UpdateAsync(task, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(new TaskId("task-001"), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(RuntimeTaskStatus.Running, stored.Status);
        Assert.Equal(1, stored.CurrentIteration);
        Assert.Equal(planningAt, stored.StartedAt);
        Assert.Equal(runningAt.AddMinutes(1), stored.UpdatedAt);
    }

    [Fact]
    public async Task Get_returns_null_when_task_does_not_exist()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteTaskRepository(context);

        var stored = await repository.GetByIdAsync(new TaskId("missing"), CancellationToken.None);

        Assert.Null(stored);
    }

    [Fact]
    public async Task ListByStatus_rejects_non_positive_limit()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteTaskRepository(context);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => repository.ListByStatusAsync(RuntimeTaskStatus.Queued, 0, CancellationToken.None));
    }

    private static RuntimeTask CreateTask(
        TaskId taskId,
        int priority = 0,
        DateTimeOffset? createdAt = null) =>
        RuntimeTask.Create(
            taskId,
            new ProjectId("project-001"),
            new MachineId("machine-001"),
            $"Task {taskId.Value}",
            "Do governed work.",
            createdAt ?? CreatedAt,
            priority: priority);

    private static async Task SeedProjectAndMachineAsync(AegesDbContext context)
    {
        var projects = new SqliteProjectRepository(context);
        var machines = new SqliteMachineRepository(context);

        await projects.AddAsync(
            RuntimeProject.Create(new ProjectId("project-001"), "Aeges", "/work/aeges", CreatedAt),
            CancellationToken.None);
        await machines.AddAsync(
            RuntimeMachine.Create(new MachineId("machine-001"), "home-laptop", "macOS arm64", CreatedAt),
            CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);
    }
}
