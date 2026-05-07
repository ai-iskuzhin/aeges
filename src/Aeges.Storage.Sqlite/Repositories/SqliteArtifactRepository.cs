using Aeges.Core;
using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite.Repositories;

/// <summary>
/// EF Core SQLite implementation of <see cref="IArtifactRepository"/>.
/// </summary>
public sealed class SqliteArtifactRepository : IArtifactRepository
{
    private readonly AegesDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteArtifactRepository"/> class.
    /// </summary>
    /// <param name="context">The Aeges SQLite database context.</param>
    public SqliteArtifactRepository(AegesDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(RuntimeArtifact artifact, CancellationToken cancellationToken)
    {
        await context.Artifacts.AddAsync(ToRecord(artifact), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RuntimeArtifact?> GetByIdAsync(ArtifactId id, CancellationToken cancellationToken)
    {
        var record = await context.Artifacts
            .AsNoTracking()
            .SingleOrDefaultAsync(artifact => artifact.Id == id.Value, cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeArtifact>> ListByTaskAsync(
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var records = await context.Artifacts
            .AsNoTracking()
            .Where(artifact => artifact.TaskId == taskId.Value)
            .ToListAsync(cancellationToken);

        return records
            .OrderBy(artifact => artifact.CreatedAt)
            .ThenBy(artifact => artifact.Id)
            .Select(ToDomain)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeArtifact>> ListByIterationAsync(
        IterationId iterationId,
        CancellationToken cancellationToken)
    {
        var records = await context.Artifacts
            .AsNoTracking()
            .Where(artifact => artifact.IterationId == iterationId.Value)
            .ToListAsync(cancellationToken);

        return records
            .OrderBy(artifact => artifact.CreatedAt)
            .ThenBy(artifact => artifact.Id)
            .Select(ToDomain)
            .ToArray();
    }

    private static ArtifactRecord ToRecord(RuntimeArtifact artifact) =>
        new()
        {
            Id = artifact.Id.Value,
            TaskId = artifact.TaskId.Value,
            IterationId = artifact.IterationId?.Value,
            Type = artifact.Type,
            RelativePath = artifact.RelativePath,
            SizeBytes = artifact.SizeBytes,
            Sha256 = artifact.Sha256,
            CreatedAt = artifact.CreatedAt,
        };

    private static RuntimeArtifact ToDomain(ArtifactRecord record) =>
        new(
            new ArtifactId(record.Id),
            new TaskId(record.TaskId),
            record.IterationId is null ? null : new IterationId(record.IterationId),
            record.Type,
            record.RelativePath,
            record.CreatedAt,
            record.SizeBytes,
            record.Sha256);
}
