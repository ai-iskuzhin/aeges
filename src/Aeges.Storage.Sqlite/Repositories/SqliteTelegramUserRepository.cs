using Aeges.Core;
using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite.Repositories;

/// <summary>
/// EF Core SQLite implementation of <see cref="ITelegramUserRepository"/>.
/// </summary>
public sealed class SqliteTelegramUserRepository : ITelegramUserRepository
{
    private readonly AegesDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteTelegramUserRepository"/> class.
    /// </summary>
    /// <param name="context">The Aeges SQLite database context.</param>
    public SqliteTelegramUserRepository(AegesDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(CancellationToken cancellationToken) =>
        await context.TelegramUsers.CountAsync(cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(RuntimeTelegramUser user, CancellationToken cancellationToken)
    {
        await context.TelegramUsers.AddAsync(ToRecord(user), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RuntimeTelegramUser?> GetByChatIdAsync(long chatId, CancellationToken cancellationToken)
    {
        var record = await context.TelegramUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.ChatId == chatId, cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    /// <inheritdoc />
    public async Task<RuntimeTelegramUser?> GetByIdAsync(TelegramUserId userId, CancellationToken cancellationToken)
    {
        var record = await context.TelegramUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == userId.Value, cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeTelegramUser>> ListAsync(CancellationToken cancellationToken)
    {
        return await context.TelegramUsers
            .AsNoTracking()
            .OrderBy(user => user.Role)
            .ThenBy(user => user.Status)
            .ThenBy(user => user.ChatId)
            .Select(user => ToDomain(user))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(RuntimeTelegramUser user, CancellationToken cancellationToken)
    {
        var record = await context.TelegramUsers
            .SingleOrDefaultAsync(existingUser => existingUser.Id == user.Id.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Telegram user '{user.Id}' was not found.");

        record.Role = user.Role;
        record.Status = user.Status;
        record.Username = user.Username;
        record.FirstName = user.FirstName;
        record.LastName = user.LastName;
        record.UpdatedAt = user.UpdatedAt;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeTelegramProjectAccess>> ListProjectAccessAsync(
        TelegramUserId userId,
        CancellationToken cancellationToken)
    {
        return await context.TelegramProjectAccess
            .AsNoTracking()
            .Where(access => access.UserId == userId.Value)
            .OrderBy(access => access.ProjectId)
            .Select(access => new RuntimeTelegramProjectAccess(
                new TelegramUserId(access.UserId),
                new ProjectId(access.ProjectId),
                access.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeTelegramProjectGroupAccess>> ListProjectGroupAccessAsync(
        TelegramUserId userId,
        CancellationToken cancellationToken)
    {
        return await context.TelegramProjectGroupAccess
            .AsNoTracking()
            .Where(access => access.UserId == userId.Value)
            .OrderBy(access => access.ProjectGroupId)
            .Select(access => new RuntimeTelegramProjectGroupAccess(
                new TelegramUserId(access.UserId),
                new ProjectGroupId(access.ProjectGroupId),
                access.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetProjectAccessAsync(
        TelegramUserId userId,
        ProjectId projectId,
        bool allowed,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        var record = await context.TelegramProjectAccess
            .SingleOrDefaultAsync(
                access => access.UserId == userId.Value && access.ProjectId == projectId.Value,
                cancellationToken);

        if (!allowed)
        {
            if (record is not null)
            {
                context.TelegramProjectAccess.Remove(record);
            }

            return;
        }

        if (record is null)
        {
            await context.TelegramProjectAccess.AddAsync(
                new TelegramProjectAccessRecord
                {
                    UserId = userId.Value,
                    ProjectId = projectId.Value,
                    CreatedAt = createdAt,
                },
                cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task SetProjectGroupAccessAsync(
        TelegramUserId userId,
        ProjectGroupId projectGroupId,
        bool allowed,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        var record = await context.TelegramProjectGroupAccess
            .SingleOrDefaultAsync(
                access => access.UserId == userId.Value && access.ProjectGroupId == projectGroupId.Value,
                cancellationToken);

        if (!allowed)
        {
            if (record is not null)
            {
                context.TelegramProjectGroupAccess.Remove(record);
            }

            return;
        }

        if (record is null)
        {
            await context.TelegramProjectGroupAccess.AddAsync(
                new TelegramProjectGroupAccessRecord
                {
                    UserId = userId.Value,
                    ProjectGroupId = projectGroupId.Value,
                    CreatedAt = createdAt,
                },
                cancellationToken);
        }
    }

    private static TelegramUserRecord ToRecord(RuntimeTelegramUser user) =>
        new()
        {
            Id = user.Id.Value,
            ChatId = user.ChatId,
            Username = user.Username,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
        };

    private static RuntimeTelegramUser ToDomain(TelegramUserRecord record) =>
        RuntimeTelegramUser.Rehydrate(
            new TelegramUserId(record.Id),
            record.ChatId,
            record.Role,
            record.Status,
            record.CreatedAt,
            record.UpdatedAt,
            new RuntimeTelegramUserProfile(record.Username, record.FirstName, record.LastName));
}
