using Aeges.Core;
using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite.Repositories;

/// <summary>
/// EF Core SQLite implementation of <see cref="IProjectRootRepository"/>.
/// </summary>
public sealed class SqliteProjectRootRepository : IProjectRootRepository
{
    private readonly AegesDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteProjectRootRepository"/> class.
    /// </summary>
    /// <param name="context">The Aeges SQLite database context.</param>
    public SqliteProjectRootRepository(AegesDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(RuntimeProjectRoot root, CancellationToken cancellationToken)
    {
        await context.ProjectRoots.AddAsync(ToRecord(root), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RuntimeProjectRoot?> GetByIdAsync(ProjectRootId id, CancellationToken cancellationToken)
    {
        var record = await context.ProjectRoots
            .AsNoTracking()
            .SingleOrDefaultAsync(root => root.Id == id.Value, cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeProjectRoot>> ListAsync(CancellationToken cancellationToken)
    {
        return await context.ProjectRoots
            .AsNoTracking()
            .OrderBy(root => root.Name)
            .ThenBy(root => root.Id)
            .Select(root => ToDomain(root))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(RuntimeProjectRoot root, CancellationToken cancellationToken)
    {
        var record = await context.ProjectRoots
            .SingleOrDefaultAsync(existingRoot => existingRoot.Id == root.Id.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Project root '{root.Id}' was not found.");

        record.Name = root.Name;
        record.Path = root.Path;
        record.UpdatedAt = root.UpdatedAt;
        record.IsArchived = root.IsArchived;
        record.ArchivedAt = root.ArchivedAt;
    }

    private static ProjectRootRecord ToRecord(RuntimeProjectRoot root) =>
        new()
        {
            Id = root.Id.Value,
            Name = root.Name,
            Path = root.Path,
            CreatedAt = root.CreatedAt,
            UpdatedAt = root.UpdatedAt,
            IsArchived = root.IsArchived,
            ArchivedAt = root.ArchivedAt,
        };

    private static RuntimeProjectRoot ToDomain(ProjectRootRecord record) =>
        RuntimeProjectRoot.Rehydrate(
            new ProjectRootId(record.Id),
            record.Name,
            record.Path,
            record.CreatedAt,
            record.UpdatedAt,
            record.IsArchived,
            record.ArchivedAt);
}
