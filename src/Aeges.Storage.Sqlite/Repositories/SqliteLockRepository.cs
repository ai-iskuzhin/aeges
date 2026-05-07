using Aeges.Core;
using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite.Repositories;

/// <summary>
/// EF Core SQLite implementation of <see cref="ILockRepository"/>.
/// </summary>
public sealed class SqliteLockRepository : ILockRepository
{
    private readonly AegesDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteLockRepository"/> class.
    /// </summary>
    /// <param name="context">The Aeges SQLite database context.</param>
    public SqliteLockRepository(AegesDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(RuntimeLock runtimeLock, CancellationToken cancellationToken)
    {
        await context.Locks.AddAsync(ToRecord(runtimeLock), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RuntimeLock?> GetByIdAsync(LockId id, CancellationToken cancellationToken)
    {
        var record = await context.Locks
            .AsNoTracking()
            .SingleOrDefaultAsync(runtimeLock => runtimeLock.Id == id.Value, cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeLock>> ListActiveByProjectAsync(
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        var records = await context.Locks
            .AsNoTracking()
            .Where(runtimeLock => runtimeLock.ProjectId == projectId.Value && runtimeLock.ReleasedAt == null)
            .ToListAsync(cancellationToken);

        return records
            .OrderBy(runtimeLock => runtimeLock.CreatedAt)
            .ThenBy(runtimeLock => runtimeLock.Id)
            .Select(ToDomain)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeLock>> ListActiveByTaskAsync(
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var records = await context.Locks
            .AsNoTracking()
            .Where(runtimeLock => runtimeLock.TaskId == taskId.Value && runtimeLock.ReleasedAt == null)
            .ToListAsync(cancellationToken);

        return records
            .OrderBy(runtimeLock => runtimeLock.CreatedAt)
            .ThenBy(runtimeLock => runtimeLock.Id)
            .Select(ToDomain)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(RuntimeLock runtimeLock, CancellationToken cancellationToken)
    {
        var record = await context.Locks
            .SingleOrDefaultAsync(existingLock => existingLock.Id == runtimeLock.Id.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Lock '{runtimeLock.Id}' was not found.");

        record.ReleasedAt = runtimeLock.ReleasedAt;
    }

    private static LockRecord ToRecord(RuntimeLock runtimeLock) =>
        new()
        {
            Id = runtimeLock.Id.Value,
            TaskId = runtimeLock.TaskId.Value,
            ProjectId = runtimeLock.ProjectId.Value,
            PathPattern = runtimeLock.PathPattern,
            CreatedAt = runtimeLock.CreatedAt,
            ReleasedAt = runtimeLock.ReleasedAt,
        };

    private static RuntimeLock ToDomain(LockRecord record) =>
        RuntimeLock.Rehydrate(
            new LockId(record.Id),
            new TaskId(record.TaskId),
            new ProjectId(record.ProjectId),
            record.PathPattern,
            record.CreatedAt,
            record.ReleasedAt);
}
