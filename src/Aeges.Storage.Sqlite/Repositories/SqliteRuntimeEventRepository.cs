using Aeges.Core;
using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite.Repositories;

/// <summary>
/// EF Core SQLite implementation of <see cref="IRuntimeEventRepository"/>.
/// </summary>
public sealed class SqliteRuntimeEventRepository : IRuntimeEventRepository
{
    private readonly AegesDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteRuntimeEventRepository"/> class.
    /// </summary>
    /// <param name="context">The Aeges SQLite database context.</param>
    public SqliteRuntimeEventRepository(AegesDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken)
    {
        await context.RuntimeEvents.AddAsync(ToRecord(runtimeEvent), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeEvent>> ListByTaskAsync(
        TaskId taskId,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be greater than zero.");
        }

        var records = await context.RuntimeEvents
            .AsNoTracking()
            .Where(runtimeEvent => runtimeEvent.TaskId == taskId.Value)
            .ToListAsync(cancellationToken);

        return records
            .OrderByDescending(runtimeEvent => runtimeEvent.CreatedAt)
            .ThenByDescending(runtimeEvent => runtimeEvent.Id)
            .Take(limit)
            .OrderBy(runtimeEvent => runtimeEvent.CreatedAt)
            .ThenBy(runtimeEvent => runtimeEvent.Id)
            .Select(ToDomain)
            .ToArray();
    }

    private static RuntimeEventRecord ToRecord(RuntimeEvent runtimeEvent) =>
        new()
        {
            Id = runtimeEvent.Id.Value,
            TaskId = runtimeEvent.TaskId?.Value,
            IterationId = runtimeEvent.IterationId?.Value,
            MachineId = runtimeEvent.MachineId?.Value,
            EventType = runtimeEvent.EventType,
            Message = runtimeEvent.Message,
            PayloadJson = runtimeEvent.PayloadJson,
            CreatedAt = runtimeEvent.CreatedAt,
        };

    private static RuntimeEvent ToDomain(RuntimeEventRecord record) =>
        RuntimeEvent.Rehydrate(
            new RuntimeEventId(record.Id),
            record.TaskId is null ? null : new TaskId(record.TaskId),
            record.IterationId is null ? null : new IterationId(record.IterationId),
            record.MachineId is null ? null : new MachineId(record.MachineId),
            record.EventType,
            record.Message,
            record.PayloadJson,
            record.CreatedAt);
}
