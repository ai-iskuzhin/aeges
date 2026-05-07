using Aeges.Core;
using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite.Repositories;

/// <summary>
/// EF Core SQLite implementation of <see cref="ITaskRepository"/>.
/// </summary>
public sealed class SqliteTaskRepository : ITaskRepository
{
    private readonly AegesDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteTaskRepository"/> class.
    /// </summary>
    /// <param name="context">The Aeges SQLite database context.</param>
    public SqliteTaskRepository(AegesDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(RuntimeTask task, CancellationToken cancellationToken)
    {
        await context.Tasks.AddAsync(ToRecord(task), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RuntimeTask?> GetByIdAsync(TaskId id, CancellationToken cancellationToken)
    {
        var record = await context.Tasks
            .AsNoTracking()
            .SingleOrDefaultAsync(task => task.Id == id.Value, cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeTask>> ListByProjectAsync(
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        var records = await context.Tasks
            .AsNoTracking()
            .Where(task => task.ProjectId == projectId.Value)
            .ToListAsync(cancellationToken);

        return records
            .OrderByDescending(task => task.Priority)
            .ThenBy(task => task.CreatedAt)
            .ThenBy(task => task.Id)
            .Select(ToDomain)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeTask>> ListByStatusAsync(
        RuntimeTaskStatus status,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be greater than zero.");
        }

        var records = await context.Tasks
            .AsNoTracking()
            .Where(task => task.Status == status)
            .ToListAsync(cancellationToken);

        return records
            .OrderByDescending(task => task.Priority)
            .ThenBy(task => task.CreatedAt)
            .ThenBy(task => task.Id)
            .Take(limit)
            .Select(ToDomain)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(RuntimeTask task, CancellationToken cancellationToken)
    {
        var record = await context.Tasks
            .SingleOrDefaultAsync(existingTask => existingTask.Id == task.Id.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Task '{task.Id}' was not found.");

        record.Status = task.Status;
        record.CurrentIteration = task.CurrentIteration;
        record.UpdatedAt = task.UpdatedAt;
        record.StartedAt = task.StartedAt;
        record.CompletedAt = task.CompletedAt;
        record.CancelledAt = task.CancelledAt;
        record.FailureReason = task.FailureReason;
    }

    private static TaskRecord ToRecord(RuntimeTask task) =>
        new()
        {
            Id = task.Id.Value,
            ProjectId = task.ProjectId.Value,
            MachineId = task.MachineId.Value,
            Title = task.Title,
            Goal = task.Goal,
            Status = task.Status,
            Priority = task.Priority,
            MaxIterations = task.MaxIterations,
            CurrentIteration = task.CurrentIteration,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            StartedAt = task.StartedAt,
            CompletedAt = task.CompletedAt,
            CancelledAt = task.CancelledAt,
            FailureReason = task.FailureReason,
        };

    private static RuntimeTask ToDomain(TaskRecord record) =>
        RuntimeTask.Rehydrate(
            new TaskId(record.Id),
            new ProjectId(record.ProjectId),
            new MachineId(record.MachineId),
            record.Title,
            record.Goal,
            record.Status,
            record.Priority,
            record.MaxIterations,
            record.CurrentIteration,
            record.CreatedAt,
            record.UpdatedAt,
            record.StartedAt,
            record.CompletedAt,
            record.CancelledAt,
            record.FailureReason);
}
