using Aeges.Core;
using Aeges.Storage.Sqlite.Repositories;

namespace Aeges.Storage.Sqlite.Tests;

public sealed class SqliteTransportCallbackActionRepositoryTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 05, 11, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task Add_get_and_update_round_trips_callback_action()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteTransportCallbackActionRepository(context);
        var action = CreateAction();

        await repository.AddAsync(action, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);
        action.MarkUsed(CreatedAt.AddMinutes(1));
        await repository.UpdateAsync(action, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetAsync("telegram", "abc123", CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal("chat:1001", stored.Scope);
        Assert.Equal(CreatedAt.AddMinutes(1), stored.LastUsedAt);
        Assert.Equal(1, stored.UseCount);
    }

    [Fact]
    public async Task PruneExpiredAsync_removes_expired_actions()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteTransportCallbackActionRepository(context);
        await repository.AddAsync(CreateAction(), CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var removed = await repository.PruneExpiredAsync(CreatedAt.AddDays(2), CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetAsync("telegram", "abc123", CancellationToken.None);
        Assert.Equal(1, removed);
        Assert.Null(stored);
    }

    private static RuntimeTransportCallbackAction CreateAction() =>
        RuntimeTransportCallbackAction.Create(
            "abc123",
            "telegram",
            "chat:1001",
            "callback",
            """{"callbackData":"ae:t"}""",
            CreatedAt,
            CreatedAt.AddDays(1));
}
