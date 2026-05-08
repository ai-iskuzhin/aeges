using Aeges.Core;
using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite.Repositories;

/// <summary>
/// EF Core SQLite implementation of <see cref="IRunnerExecutionRepository"/>.
/// </summary>
public sealed class SqliteRunnerExecutionRepository : IRunnerExecutionRepository
{
    private readonly AegesDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteRunnerExecutionRepository"/> class.
    /// </summary>
    /// <param name="context">The Aeges SQLite database context.</param>
    public SqliteRunnerExecutionRepository(AegesDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(RuntimeRunnerExecution execution, CancellationToken cancellationToken)
    {
        await context.RunnerExecutions.AddAsync(ToRecord(execution), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RuntimeRunnerExecution?> GetByIdAsync(
        RunnerExecutionId id,
        CancellationToken cancellationToken)
    {
        var record = await context.RunnerExecutions
            .AsNoTracking()
            .SingleOrDefaultAsync(execution => execution.Id == id.Value, cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeRunnerExecution>> ListByTaskAsync(
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var records = await context.RunnerExecutions
            .AsNoTracking()
            .Where(execution => execution.TaskId == taskId.Value)
            .ToListAsync(cancellationToken);

        return records
            .OrderBy(execution => execution.StartedAt)
            .ThenBy(execution => execution.Id)
            .Select(ToDomain)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeRunnerExecution>> ListByIterationAsync(
        IterationId iterationId,
        CancellationToken cancellationToken)
    {
        var records = await context.RunnerExecutions
            .AsNoTracking()
            .Where(execution => execution.IterationId == iterationId.Value)
            .ToListAsync(cancellationToken);

        return records
            .OrderBy(execution => execution.StartedAt)
            .ThenBy(execution => execution.Id)
            .Select(ToDomain)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(RuntimeRunnerExecution execution, CancellationToken cancellationToken)
    {
        var record = await context.RunnerExecutions
            .SingleOrDefaultAsync(existingExecution => existingExecution.Id == execution.Id.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Runner execution '{execution.Id}' was not found.");

        record.ExitCode = execution.ExitCode;
        record.CompletedAt = execution.CompletedAt;
        record.TimedOut = execution.TimedOut;
        record.Cancelled = execution.Cancelled;
    }

    private static RunnerExecutionRecord ToRecord(RuntimeRunnerExecution execution) =>
        new()
        {
            Id = execution.Id.Value,
            TaskId = execution.TaskId.Value,
            IterationId = execution.IterationId.Value,
            RunnerId = execution.RunnerId.Value,
            Command = execution.Command,
            WorkingDirectory = execution.WorkingDirectory,
            ExitCode = execution.ExitCode,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            TimedOut = execution.TimedOut,
            Cancelled = execution.Cancelled,
        };

    private static RuntimeRunnerExecution ToDomain(RunnerExecutionRecord record) =>
        RuntimeRunnerExecution.Rehydrate(
            new RunnerExecutionId(record.Id),
            new TaskId(record.TaskId),
            new IterationId(record.IterationId),
            new RunnerId(record.RunnerId),
            record.Command,
            record.WorkingDirectory,
            record.ExitCode,
            record.StartedAt,
            record.CompletedAt,
            record.TimedOut,
            record.Cancelled);
}
