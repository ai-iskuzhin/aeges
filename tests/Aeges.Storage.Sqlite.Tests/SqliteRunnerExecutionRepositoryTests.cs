using Aeges.Core;
using Aeges.Storage.Sqlite.Repositories;

namespace Aeges.Storage.Sqlite.Tests;

public sealed class SqliteRunnerExecutionRepositoryTests
{
    [Fact]
    public async Task Add_and_get_round_trips_runner_execution()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SeedTaskAndIterationAsync(context);
        var repository = new SqliteRunnerExecutionRepository(context);
        var execution = CreateExecution(new RunnerExecutionId("runner-execution-001"));
        execution.RecordExit(0, SqliteRepositorySeed.CreatedAt.AddMinutes(2));

        await repository.AddAsync(execution, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(new RunnerExecutionId("runner-execution-001"), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(execution.Id, stored.Id);
        Assert.Equal(execution.TaskId, stored.TaskId);
        Assert.Equal(execution.IterationId, stored.IterationId);
        Assert.Equal(execution.RunnerId, stored.RunnerId);
        Assert.Equal(execution.Command, stored.Command);
        Assert.Equal(execution.WorkingDirectory, stored.WorkingDirectory);
        Assert.Equal(execution.ExitCode, stored.ExitCode);
        Assert.Equal(execution.StartedAt, stored.StartedAt);
        Assert.Equal(execution.CompletedAt, stored.CompletedAt);
        Assert.Equal(execution.TimedOut, stored.TimedOut);
        Assert.Equal(execution.Cancelled, stored.Cancelled);
    }

    [Fact]
    public async Task List_methods_return_runner_executions_ordered_by_start_time()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SeedTaskAndIterationAsync(context);
        var repository = new SqliteRunnerExecutionRepository(context);

        await repository.AddAsync(
            CreateExecution(
                new RunnerExecutionId("runner-execution-002"),
                SqliteRepositorySeed.CreatedAt.AddMinutes(2)),
            CancellationToken.None);
        await repository.AddAsync(
            CreateExecution(
                new RunnerExecutionId("runner-execution-001"),
                SqliteRepositorySeed.CreatedAt.AddMinutes(1)),
            CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var byTask = await repository.ListByTaskAsync(new TaskId("task-001"), CancellationToken.None);
        var byIteration = await repository.ListByIterationAsync(new IterationId("iteration-001"), CancellationToken.None);

        Assert.Collection(
            byTask,
            execution => Assert.Equal(new RunnerExecutionId("runner-execution-001"), execution.Id),
            execution => Assert.Equal(new RunnerExecutionId("runner-execution-002"), execution.Id));
        Assert.Collection(
            byIteration,
            execution => Assert.Equal(new RunnerExecutionId("runner-execution-001"), execution.Id),
            execution => Assert.Equal(new RunnerExecutionId("runner-execution-002"), execution.Id));
    }

    [Fact]
    public async Task Update_persists_runner_execution_completion()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SeedTaskAndIterationAsync(context);
        var repository = new SqliteRunnerExecutionRepository(context);
        var execution = CreateExecution(new RunnerExecutionId("runner-execution-001"));
        await repository.AddAsync(execution, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        execution.RecordTimeout(SqliteRepositorySeed.CreatedAt.AddMinutes(30));
        await repository.UpdateAsync(execution, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(new RunnerExecutionId("runner-execution-001"), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.True(stored.IsCompleted);
        Assert.True(stored.TimedOut);
        Assert.False(stored.Cancelled);
        Assert.Null(stored.ExitCode);
        Assert.Equal(SqliteRepositorySeed.CreatedAt.AddMinutes(30), stored.CompletedAt);
    }

    [Fact]
    public async Task Get_returns_null_when_runner_execution_does_not_exist()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteRunnerExecutionRepository(context);

        var stored = await repository.GetByIdAsync(new RunnerExecutionId("missing"), CancellationToken.None);

        Assert.Null(stored);
    }

    private static RuntimeRunnerExecution CreateExecution(
        RunnerExecutionId id,
        DateTimeOffset? startedAt = null) =>
        RuntimeRunnerExecution.Start(
            id,
            new TaskId("task-001"),
            new IterationId("iteration-001"),
            new RunnerId("codex"),
            "codex exec --json prompt.md",
            "/work/aeges",
            startedAt ?? SqliteRepositorySeed.CreatedAt);

    private static async Task SeedTaskAndIterationAsync(AegesDbContext context)
    {
        await SqliteRepositorySeed.SeedProjectMachineAndTaskAsync(context);
        var iterations = new SqliteIterationRepository(context);
        await iterations.AddAsync(
            TaskIteration.Create(
                new IterationId("iteration-001"),
                new TaskId("task-001"),
                1,
                new RunnerId("codex"),
                SqliteRepositorySeed.CreatedAt),
            CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);
    }
}
