using Aeges.Core;
using Aeges.Storage.Sqlite.Repositories;

namespace Aeges.Storage.Sqlite.Tests;

public sealed class SqliteLockRepositoryTests
{
    [Fact]
    public async Task Add_and_get_round_trips_lock()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SqliteRepositorySeed.SeedProjectMachineAndTaskAsync(context);
        var repository = new SqliteLockRepository(context);
        var runtimeLock = CreateLock(new LockId("lock-001"), "src/Auth/*");

        await repository.AddAsync(runtimeLock, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(new LockId("lock-001"), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(runtimeLock.Id, stored.Id);
        Assert.Equal(runtimeLock.TaskId, stored.TaskId);
        Assert.Equal(runtimeLock.ProjectId, stored.ProjectId);
        Assert.Equal(runtimeLock.PathPattern, stored.PathPattern);
        Assert.Equal(runtimeLock.CreatedAt, stored.CreatedAt);
        Assert.True(stored.IsActive);
    }

    [Fact]
    public async Task Lists_only_active_locks()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SqliteRepositorySeed.SeedProjectMachineAndTaskAsync(context);
        var repository = new SqliteLockRepository(context);
        var releasedLock = CreateLock(new LockId("lock-released"), "src/Old/*", SqliteRepositorySeed.CreatedAt.AddMinutes(1));
        releasedLock.Release(SqliteRepositorySeed.CreatedAt.AddMinutes(2));

        await repository.AddAsync(CreateLock(new LockId("lock-active"), "src/Auth/*"), CancellationToken.None);
        await repository.AddAsync(releasedLock, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var projectLocks = await repository.ListActiveByProjectAsync(new ProjectId("project-001"), CancellationToken.None);
        var taskLocks = await repository.ListActiveByTaskAsync(new TaskId("task-001"), CancellationToken.None);

        Assert.Single(projectLocks);
        Assert.Single(taskLocks);
        Assert.Equal(new LockId("lock-active"), projectLocks[0].Id);
        Assert.Equal(new LockId("lock-active"), taskLocks[0].Id);
    }

    [Fact]
    public async Task Update_persists_release()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SqliteRepositorySeed.SeedProjectMachineAndTaskAsync(context);
        var repository = new SqliteLockRepository(context);
        var runtimeLock = CreateLock(new LockId("lock-001"), "src/Auth/*");
        await repository.AddAsync(runtimeLock, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var releasedAt = SqliteRepositorySeed.CreatedAt.AddMinutes(3);
        runtimeLock.Release(releasedAt);
        await repository.UpdateAsync(runtimeLock, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(new LockId("lock-001"), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.False(stored.IsActive);
        Assert.Equal(releasedAt, stored.ReleasedAt);
    }

    [Fact]
    public async Task Get_returns_null_when_lock_does_not_exist()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteLockRepository(context);

        var stored = await repository.GetByIdAsync(new LockId("missing"), CancellationToken.None);

        Assert.Null(stored);
    }

    private static RuntimeLock CreateLock(
        LockId id,
        string pathPattern,
        DateTimeOffset? createdAt = null) =>
        RuntimeLock.Acquire(
            id,
            new TaskId("task-001"),
            new ProjectId("project-001"),
            pathPattern,
            createdAt ?? SqliteRepositorySeed.CreatedAt);
}
