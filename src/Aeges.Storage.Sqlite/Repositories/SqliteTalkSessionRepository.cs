using Aeges.Core;
using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite.Repositories;

/// <summary>
/// EF Core SQLite implementation of <see cref="ITalkSessionRepository"/>.
/// </summary>
public sealed class SqliteTalkSessionRepository : ITalkSessionRepository
{
    private readonly AegesDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteTalkSessionRepository"/> class.
    /// </summary>
    /// <param name="context">The Aeges SQLite database context.</param>
    public SqliteTalkSessionRepository(AegesDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(RuntimeTalkSession session, CancellationToken cancellationToken)
    {
        await context.TalkSessions.AddAsync(ToRecord(session), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RuntimeTalkSession?> GetByIdAsync(TalkSessionId id, CancellationToken cancellationToken)
    {
        var record = await context.TalkSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(session => session.Id == id.Value, cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    /// <inheritdoc />
    public async Task<RuntimeTalkSession?> GetLatestOpenBySourceAsync(
        string source,
        CancellationToken cancellationToken)
    {
        var records = await context.TalkSessions
            .AsNoTracking()
            .Where(session => session.Source == source && session.Status == TalkSessionStatus.Open.ToStorageValue())
            .ToListAsync(cancellationToken);
        var record = records
            .OrderByDescending(session => session.UpdatedAt)
            .ThenByDescending(session => session.CreatedAt)
            .FirstOrDefault();

        return record is null ? null : ToDomain(record);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeTalkSession>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var records = await context.TalkSessions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return [.. records
            .OrderByDescending(session => session.UpdatedAt)
            .ThenByDescending(session => session.CreatedAt)
            .Take(limit)
            .Select(ToDomain)];
    }

    /// <inheritdoc />
    public async Task UpdateAsync(RuntimeTalkSession session, CancellationToken cancellationToken)
    {
        var record = await context.TalkSessions
            .SingleOrDefaultAsync(existing => existing.Id == session.Id.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Talk session '{session.Id}' was not found.");

        record.Title = session.Title;
        record.Status = session.Status.ToStorageValue();
        record.ExternalSessionId = session.ExternalSessionId;
        record.UpdatedAt = session.UpdatedAt;
        record.ArchivedAt = session.ArchivedAt;
    }

    private static TalkSessionRecord ToRecord(RuntimeTalkSession session) =>
        new()
        {
            Id = session.Id.Value,
            Source = session.Source,
            Title = session.Title,
            RunnerId = session.RunnerId.Value,
            Status = session.Status.ToStorageValue(),
            ExternalSessionId = session.ExternalSessionId,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt,
            ArchivedAt = session.ArchivedAt,
        };

    private static RuntimeTalkSession ToDomain(TalkSessionRecord record) =>
        RuntimeTalkSession.Rehydrate(
            new TalkSessionId(record.Id),
            record.Source,
            record.Title,
            new RunnerId(record.RunnerId),
            TalkSessionStatusExtensions.FromStorageValue(record.Status),
            record.ExternalSessionId,
            record.CreatedAt,
            record.UpdatedAt,
            record.ArchivedAt);
}
