using Aeges.Core;
using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite.Repositories;

/// <summary>
/// EF Core SQLite implementation of <see cref="IProjectRepository"/>.
/// </summary>
public sealed class SqliteProjectRepository : IProjectRepository
{
    private readonly AegesDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteProjectRepository"/> class.
    /// </summary>
    /// <param name="context">The Aeges SQLite database context.</param>
    public SqliteProjectRepository(AegesDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(RuntimeProject project, CancellationToken cancellationToken)
    {
        await context.Projects.AddAsync(ToRecord(project), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RuntimeProject?> GetByIdAsync(ProjectId id, CancellationToken cancellationToken)
    {
        var record = await context.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(project => project.Id == id.Value, cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeProject>> ListAsync(CancellationToken cancellationToken)
    {
        return await context.Projects
            .AsNoTracking()
            .OrderBy(project => project.Name)
            .ThenBy(project => project.Id)
            .Select(project => ToDomain(project))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(RuntimeProject project, CancellationToken cancellationToken)
    {
        var record = await context.Projects
            .SingleOrDefaultAsync(existingProject => existingProject.Id == project.Id.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Project '{project.Id}' was not found.");

        record.Name = project.Name;
        record.Path = project.Path;
        record.GroupId = project.GroupId?.Value;
        record.UpdatedAt = project.UpdatedAt;
        record.IsArchived = project.IsArchived;
        record.ArchivedAt = project.ArchivedAt;
    }

    private static ProjectRecord ToRecord(RuntimeProject project) =>
        new()
        {
            Id = project.Id.Value,
            Name = project.Name,
            Path = project.Path,
            GroupId = project.GroupId?.Value,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            IsArchived = project.IsArchived,
            ArchivedAt = project.ArchivedAt,
        };

    private static RuntimeProject ToDomain(ProjectRecord record) =>
        RuntimeProject.Rehydrate(
            new ProjectId(record.Id),
            record.Name,
            record.Path,
            record.CreatedAt,
            record.UpdatedAt,
            record.GroupId is null ? null : new ProjectGroupId(record.GroupId),
            record.IsArchived,
            record.ArchivedAt);
}
