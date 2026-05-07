using Aeges.Core;
using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite.Repositories;

/// <summary>
/// EF Core SQLite implementation of <see cref="IMachineRepository"/>.
/// </summary>
public sealed class SqliteMachineRepository : IMachineRepository
{
    private readonly AegesDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteMachineRepository"/> class.
    /// </summary>
    /// <param name="context">The Aeges SQLite database context.</param>
    public SqliteMachineRepository(AegesDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(RuntimeMachine machine, CancellationToken cancellationToken)
    {
        await context.Machines.AddAsync(ToRecord(machine), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RuntimeMachine?> GetByIdAsync(MachineId id, CancellationToken cancellationToken)
    {
        var record = await context.Machines
            .AsNoTracking()
            .SingleOrDefaultAsync(machine => machine.Id == id.Value, cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeMachine>> ListAsync(CancellationToken cancellationToken)
    {
        return await context.Machines
            .AsNoTracking()
            .OrderBy(machine => machine.Name)
            .ThenBy(machine => machine.Id)
            .Select(machine => ToDomain(machine))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(RuntimeMachine machine, CancellationToken cancellationToken)
    {
        var record = await context.Machines
            .SingleOrDefaultAsync(existingMachine => existingMachine.Id == machine.Id.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Machine '{machine.Id}' was not found.");

        record.Name = machine.Name;
        record.Platform = machine.Platform;
        record.Status = machine.Status.ToStorageValue();
        record.LastSeenAt = machine.LastSeenAt;
        record.UpdatedAt = machine.UpdatedAt;
    }

    private static MachineRecord ToRecord(RuntimeMachine machine) =>
        new()
        {
            Id = machine.Id.Value,
            Name = machine.Name,
            Platform = machine.Platform,
            Status = machine.Status.ToStorageValue(),
            LastSeenAt = machine.LastSeenAt,
            CreatedAt = machine.CreatedAt,
            UpdatedAt = machine.UpdatedAt,
        };

    private static RuntimeMachine ToDomain(MachineRecord record) =>
        RuntimeMachine.Rehydrate(
            new MachineId(record.Id),
            record.Name,
            record.Platform,
            MachineStatusExtensions.FromStorageValue(record.Status),
            record.LastSeenAt,
            record.CreatedAt,
            record.UpdatedAt);
}
