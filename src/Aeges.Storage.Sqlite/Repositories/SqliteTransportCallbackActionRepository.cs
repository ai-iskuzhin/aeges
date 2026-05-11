using Aeges.Core;
using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite.Repositories;

/// <summary>
/// EF Core SQLite implementation of <see cref="ITransportCallbackActionRepository"/>.
/// </summary>
public sealed class SqliteTransportCallbackActionRepository : ITransportCallbackActionRepository
{
    private readonly AegesDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteTransportCallbackActionRepository"/> class.
    /// </summary>
    /// <param name="context">The Aeges SQLite database context.</param>
    public SqliteTransportCallbackActionRepository(AegesDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(RuntimeTransportCallbackAction action, CancellationToken cancellationToken)
    {
        await context.TransportCallbackActions.AddAsync(ToRecord(action), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RuntimeTransportCallbackAction?> GetAsync(
        string transport,
        string token,
        CancellationToken cancellationToken)
    {
        var record = await context.TransportCallbackActions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                action => action.Transport == transport && action.Token == token,
                cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(RuntimeTransportCallbackAction action, CancellationToken cancellationToken)
    {
        var record = await context.TransportCallbackActions
            .SingleOrDefaultAsync(
                existing => existing.Transport == action.Transport && existing.Token == action.Token,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Transport callback action '{action.Transport}:{action.Token}' was not found.");

        record.Scope = action.Scope;
        record.ActionType = action.ActionType;
        record.PayloadJson = action.PayloadJson;
        record.ExpiresAt = action.ExpiresAt;
        record.LastUsedAt = action.LastUsedAt;
        record.UseCount = action.UseCount;
    }

    /// <inheritdoc />
    public async Task<int> PruneExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var records = await context.TransportCallbackActions
            .ToListAsync(cancellationToken);
        var expired = records
            .Where(action => action.ExpiresAt <= now)
            .ToArray();

        context.TransportCallbackActions.RemoveRange(expired);

        return expired.Length;
    }

    private static TransportCallbackActionRecord ToRecord(RuntimeTransportCallbackAction action) =>
        new()
        {
            Token = action.Token,
            Transport = action.Transport,
            Scope = action.Scope,
            ActionType = action.ActionType,
            PayloadJson = action.PayloadJson,
            CreatedAt = action.CreatedAt,
            ExpiresAt = action.ExpiresAt,
            LastUsedAt = action.LastUsedAt,
            UseCount = action.UseCount,
        };

    private static RuntimeTransportCallbackAction ToDomain(TransportCallbackActionRecord record) =>
        RuntimeTransportCallbackAction.Rehydrate(
            record.Token,
            record.Transport,
            record.Scope,
            record.ActionType,
            record.PayloadJson,
            record.CreatedAt,
            record.ExpiresAt,
            record.LastUsedAt,
            record.UseCount);
}
