using Aeges.Core;
using Aeges.Storage.Sqlite.Repositories;

namespace Aeges.Storage.Sqlite.Tests;

public sealed class SqliteIterationRepositoryTests
{
    [Fact]
    public async Task Add_and_get_round_trips_iteration()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SqliteRepositorySeed.SeedProjectMachineAndTaskAsync(context);
        var repository = new SqliteIterationRepository(context);
        var iteration = CreateIteration(new IterationId("iteration-001"), 1);

        await repository.AddAsync(iteration, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(new IterationId("iteration-001"), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(iteration.Id, stored.Id);
        Assert.Equal(iteration.TaskId, stored.TaskId);
        Assert.Equal(iteration.IterationNumber, stored.IterationNumber);
        Assert.Equal(iteration.Status, stored.Status);
        Assert.Equal(iteration.RunnerId, stored.RunnerId);
        Assert.Equal(iteration.CreatedAt, stored.CreatedAt);
        Assert.Equal(iteration.UpdatedAt, stored.UpdatedAt);
    }

    [Fact]
    public async Task ListByTask_orders_iterations_by_number()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SqliteRepositorySeed.SeedProjectMachineAndTaskAsync(context);
        var repository = new SqliteIterationRepository(context);

        await repository.AddAsync(CreateIteration(new IterationId("iteration-002"), 2), CancellationToken.None);
        await repository.AddAsync(CreateIteration(new IterationId("iteration-001"), 1), CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var iterations = await repository.ListByTaskAsync(new TaskId("task-001"), CancellationToken.None);

        Assert.Collection(
            iterations,
            iteration => Assert.Equal(new IterationId("iteration-001"), iteration.Id),
            iteration => Assert.Equal(new IterationId("iteration-002"), iteration.Id));
    }

    [Fact]
    public async Task Update_persists_iteration_state()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SqliteRepositorySeed.SeedProjectMachineAndTaskAsync(context);
        var repository = new SqliteIterationRepository(context);
        var iteration = CreateIteration(new IterationId("iteration-001"), 1);
        await repository.AddAsync(iteration, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var runningAt = SqliteRepositorySeed.CreatedAt.AddMinutes(1);
        iteration.AssignWorktree("/tmp/aeges/worktrees/project-001/task-001", runningAt);
        iteration.AttachPromptArtifact(new ArtifactId("artifact-prompt"), runningAt.AddMinutes(1));
        iteration.StartRunning(runningAt.AddMinutes(2));
        await repository.UpdateAsync(iteration, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(new IterationId("iteration-001"), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(TaskIterationStatus.Running, stored.Status);
        Assert.Equal("/tmp/aeges/worktrees/project-001/task-001", stored.WorktreePath);
        Assert.Equal(new ArtifactId("artifact-prompt"), stored.PromptArtifactId);
        Assert.Equal(runningAt.AddMinutes(2), stored.StartedAt);
        Assert.Equal(runningAt.AddMinutes(2), stored.UpdatedAt);
    }

    [Fact]
    public async Task Get_returns_null_when_iteration_does_not_exist()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteIterationRepository(context);

        var stored = await repository.GetByIdAsync(new IterationId("missing"), CancellationToken.None);

        Assert.Null(stored);
    }

    private static TaskIteration CreateIteration(IterationId id, int iterationNumber) =>
        TaskIteration.Create(
            id,
            new TaskId("task-001"),
            iterationNumber,
            new RunnerId("codex"),
            SqliteRepositorySeed.CreatedAt);
}
