using Aeges.Core;
using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite.Repositories;

/// <summary>
/// EF Core SQLite implementation of <see cref="IIterationRepository"/>.
/// </summary>
public sealed class SqliteIterationRepository : IIterationRepository
{
    private readonly AegesDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteIterationRepository"/> class.
    /// </summary>
    /// <param name="context">The Aeges SQLite database context.</param>
    public SqliteIterationRepository(AegesDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(TaskIteration iteration, CancellationToken cancellationToken)
    {
        await context.TaskIterations.AddAsync(ToRecord(iteration), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TaskIteration?> GetByIdAsync(IterationId id, CancellationToken cancellationToken)
    {
        var record = await context.TaskIterations
            .AsNoTracking()
            .SingleOrDefaultAsync(iteration => iteration.Id == id.Value, cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TaskIteration>> ListByTaskAsync(
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        return await context.TaskIterations
            .AsNoTracking()
            .Where(iteration => iteration.TaskId == taskId.Value)
            .OrderBy(iteration => iteration.IterationNumber)
            .ThenBy(iteration => iteration.Id)
            .Select(iteration => ToDomain(iteration))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(TaskIteration iteration, CancellationToken cancellationToken)
    {
        var record = await context.TaskIterations
            .SingleOrDefaultAsync(existingIteration => existingIteration.Id == iteration.Id.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Iteration '{iteration.Id}' was not found.");

        record.Status = iteration.Status;
        record.WorktreePath = iteration.WorktreePath;
        record.PromptArtifactId = iteration.PromptArtifactId?.Value;
        record.ResultArtifactId = iteration.ResultArtifactId?.Value;
        record.DiffArtifactId = iteration.DiffArtifactId?.Value;
        record.UpdatedAt = iteration.UpdatedAt;
        record.StartedAt = iteration.StartedAt;
        record.CompletedAt = iteration.CompletedAt;
        record.FailureReason = iteration.FailureReason;
    }

    private static TaskIterationRecord ToRecord(TaskIteration iteration) =>
        new()
        {
            Id = iteration.Id.Value,
            TaskId = iteration.TaskId.Value,
            IterationNumber = iteration.IterationNumber,
            Status = iteration.Status,
            RunnerId = iteration.RunnerId.Value,
            WorktreePath = iteration.WorktreePath,
            PromptArtifactId = iteration.PromptArtifactId?.Value,
            ResultArtifactId = iteration.ResultArtifactId?.Value,
            DiffArtifactId = iteration.DiffArtifactId?.Value,
            CreatedAt = iteration.CreatedAt,
            UpdatedAt = iteration.UpdatedAt,
            StartedAt = iteration.StartedAt,
            CompletedAt = iteration.CompletedAt,
            FailureReason = iteration.FailureReason,
        };

    private static TaskIteration ToDomain(TaskIterationRecord record) =>
        TaskIteration.Rehydrate(
            new IterationId(record.Id),
            new TaskId(record.TaskId),
            record.IterationNumber,
            record.Status,
            new RunnerId(record.RunnerId),
            record.WorktreePath,
            record.PromptArtifactId is null ? null : new ArtifactId(record.PromptArtifactId),
            record.ResultArtifactId is null ? null : new ArtifactId(record.ResultArtifactId),
            record.DiffArtifactId is null ? null : new ArtifactId(record.DiffArtifactId),
            record.CreatedAt,
            record.UpdatedAt,
            record.StartedAt,
            record.CompletedAt,
            record.FailureReason);
}
