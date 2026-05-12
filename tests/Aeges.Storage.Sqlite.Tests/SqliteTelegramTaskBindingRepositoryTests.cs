using Aeges.Core;
using Aeges.Storage.Sqlite.Repositories;

namespace Aeges.Storage.Sqlite.Tests;

public sealed class SqliteTelegramTaskBindingRepositoryTests
{
    [Fact]
    public async Task Upsert_and_list_round_trip_topic_binding()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SqliteRepositorySeed.SeedProjectMachineAndTaskAsync(context);
        var repository = new SqliteTelegramTaskBindingRepository(context);
        var binding = RuntimeTelegramTaskBinding.Create(
            -1001,
            77,
            new TaskId("task-001"),
            detailMessageId: 9001,
            SqliteRepositorySeed.CreatedAt);

        await repository.UpsertAsync(binding, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.ListAsync(CancellationToken.None);

        Assert.Single(stored);
        Assert.Equal(binding.ChatId, stored[0].ChatId);
        Assert.Equal(binding.MessageThreadId, stored[0].MessageThreadId);
        Assert.Equal(binding.TaskId, stored[0].TaskId);
        Assert.Equal(binding.DetailMessageId, stored[0].DetailMessageId);
    }

    [Fact]
    public async Task Upsert_updates_existing_binding()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SqliteRepositorySeed.SeedProjectMachineAndTaskAsync(context);
        var repository = new SqliteTelegramTaskBindingRepository(context);

        await repository.UpsertAsync(
            RuntimeTelegramTaskBinding.Create(-1001, 77, new TaskId("task-001"), 9001, SqliteRepositorySeed.CreatedAt),
            CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        await repository.UpsertAsync(
            RuntimeTelegramTaskBinding.Create(
                -1001,
                77,
                new TaskId("task-001"),
                detailMessageId: 9002,
                SqliteRepositorySeed.CreatedAt.AddMinutes(1)),
            CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.ListAsync(CancellationToken.None);

        Assert.Single(stored);
        Assert.Equal(9002, stored[0].DetailMessageId);
    }

    [Fact]
    public async Task Private_chat_binding_round_trips_without_thread()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SqliteRepositorySeed.SeedProjectMachineAndTaskAsync(context);
        var repository = new SqliteTelegramTaskBindingRepository(context);

        await repository.UpsertAsync(
            RuntimeTelegramTaskBinding.Create(
                1001,
                messageThreadId: null,
                new TaskId("task-001"),
                detailMessageId: null,
                SqliteRepositorySeed.CreatedAt),
            CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.ListAsync(CancellationToken.None);

        Assert.Single(stored);
        Assert.Null(stored[0].MessageThreadId);
    }

    [Fact]
    public async Task Delete_removes_binding()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SqliteRepositorySeed.SeedProjectMachineAndTaskAsync(context);
        var repository = new SqliteTelegramTaskBindingRepository(context);

        await repository.UpsertAsync(
            RuntimeTelegramTaskBinding.Create(-1001, 77, new TaskId("task-001"), 9001, SqliteRepositorySeed.CreatedAt),
            CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        await repository.DeleteAsync(-1001, 77, new TaskId("task-001"), CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.ListAsync(CancellationToken.None);

        Assert.Empty(stored);
    }
}
