using Aeges.Core;
using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite.Repositories;

/// <summary>
/// EF Core SQLite implementation of <see cref="IProjectGroupRepository"/>.
/// </summary>
public sealed class SqliteProjectGroupRepository : IProjectGroupRepository
{
    private readonly AegesDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteProjectGroupRepository"/> class.
    /// </summary>
    /// <param name="context">The Aeges SQLite database context.</param>
    public SqliteProjectGroupRepository(AegesDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(RuntimeProjectGroup group, CancellationToken cancellationToken)
    {
        await context.ProjectGroups.AddAsync(ToRecord(group), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RuntimeProjectGroup?> GetByIdAsync(ProjectGroupId id, CancellationToken cancellationToken)
    {
        var record = await context.ProjectGroups
            .AsNoTracking()
            .SingleOrDefaultAsync(group => group.Id == id.Value, cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeProjectGroup>> ListAsync(CancellationToken cancellationToken)
    {
        return await context.ProjectGroups
            .AsNoTracking()
            .OrderBy(group => group.Name)
            .ThenBy(group => group.Id)
            .Select(group => ToDomain(group))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(RuntimeProjectGroup group, CancellationToken cancellationToken)
    {
        var record = await context.ProjectGroups
            .SingleOrDefaultAsync(existingGroup => existingGroup.Id == group.Id.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Project group '{group.Id}' was not found.");

        record.Name = group.Name;
        record.Path = group.Path;
        record.UpdatedAt = group.UpdatedAt;
        record.IsArchived = group.IsArchived;
        record.ArchivedAt = group.ArchivedAt;
    }

    private static ProjectGroupRecord ToRecord(RuntimeProjectGroup group) =>
        new()
        {
            Id = group.Id.Value,
            Name = group.Name,
            Path = group.Path,
            CreatedAt = group.CreatedAt,
            UpdatedAt = group.UpdatedAt,
            IsArchived = group.IsArchived,
            ArchivedAt = group.ArchivedAt,
        };

    private static RuntimeProjectGroup ToDomain(ProjectGroupRecord record) =>
        RuntimeProjectGroup.Rehydrate(
            new ProjectGroupId(record.Id),
            record.Name,
            record.CreatedAt,
            record.UpdatedAt,
            record.Path,
            record.IsArchived,
            record.ArchivedAt);
}
