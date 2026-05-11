using Aeges.Core;
using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite.Repositories;

/// <summary>
/// EF Core SQLite implementation of <see cref="ITalkMessageRepository"/>.
/// </summary>
public sealed class SqliteTalkMessageRepository : ITalkMessageRepository
{
    private readonly AegesDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteTalkMessageRepository"/> class.
    /// </summary>
    /// <param name="context">The Aeges SQLite database context.</param>
    public SqliteTalkMessageRepository(AegesDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(RuntimeTalkMessage message, CancellationToken cancellationToken)
    {
        await context.TalkMessages.AddAsync(ToRecord(message), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeTalkMessage>> ListBySessionAsync(
        TalkSessionId sessionId,
        CancellationToken cancellationToken)
    {
        var records = await context.TalkMessages
            .AsNoTracking()
            .Where(message => message.SessionId == sessionId.Value)
            .ToListAsync(cancellationToken);

        return [.. records
            .OrderBy(message => message.CreatedAt)
            .ThenBy(message => message.Id)
            .Select(ToDomain)];
    }

    private static TalkMessageRecord ToRecord(RuntimeTalkMessage message) =>
        new()
        {
            Id = message.Id.Value,
            SessionId = message.SessionId.Value,
            Role = message.Role.ToStorageValue(),
            Content = message.Content,
            CreatedAt = message.CreatedAt,
        };

    private static RuntimeTalkMessage ToDomain(TalkMessageRecord record) =>
        RuntimeTalkMessage.Create(
            new TalkMessageId(record.Id),
            new TalkSessionId(record.SessionId),
            TalkMessageRoleExtensions.FromStorageValue(record.Role),
            record.Content,
            record.CreatedAt);
}
