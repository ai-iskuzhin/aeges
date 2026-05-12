using Aeges.Core;
using Aeges.Storage.Sqlite.Repositories;

namespace Aeges.Storage.Sqlite.Tests;

public sealed class SqliteTelegramUserRepositoryTests
{
    [Fact]
    public async Task AddAsync_persists_telegram_user()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteTelegramUserRepository(context);
        var user = RuntimeTelegramUser.CreateFirstAdmin(
            1001,
            SqliteRepositorySeed.CreatedAt,
            new RuntimeTelegramUserProfile("aigiz", "Aigiz", "Iskuzhin"));

        await repository.AddAsync(user, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByChatIdAsync(1001, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(user.Id, stored.Id);
        Assert.Equal("aigiz", stored.Username);
        Assert.Equal("Aigiz", stored.FirstName);
        Assert.Equal("Iskuzhin", stored.LastName);
        Assert.Equal(TelegramUserRole.Admin, stored.Role);
        Assert.Equal(TelegramUserStatus.Approved, stored.Status);
    }

    [Fact]
    public async Task UpdateAsync_refreshes_telegram_user_profile()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteTelegramUserRepository(context);
        var user = RuntimeTelegramUser.CreatePending(2002, SqliteRepositorySeed.CreatedAt);

        await repository.AddAsync(user, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        user.UpdateProfile(
            new RuntimeTelegramUserProfile("new-user", "New", "User"),
            SqliteRepositorySeed.CreatedAt.AddMinutes(1));
        await repository.UpdateAsync(user, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByChatIdAsync(2002, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal("new-user", stored.Username);
        Assert.Equal("New", stored.FirstName);
        Assert.Equal("User", stored.LastName);
    }

    [Fact]
    public async Task Set_access_methods_toggle_sparse_grants()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var users = new SqliteTelegramUserRepository(context);
        var groups = new SqliteProjectGroupRepository(context);
        var projects = new SqliteProjectRepository(context);
        var user = RuntimeTelegramUser.CreatePending(2002, SqliteRepositorySeed.CreatedAt);
        var groupId = new ProjectGroupId("analitex");
        var projectId = new ProjectId("project-aeges");

        await users.AddAsync(user, CancellationToken.None);
        await groups.AddAsync(RuntimeProjectGroup.Create(groupId, "Analitex", SqliteRepositorySeed.CreatedAt), CancellationToken.None);
        await projects.AddAsync(
            RuntimeProject.Create(projectId, "Aeges", "/work/aeges", SqliteRepositorySeed.CreatedAt, groupId),
            CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        await users.SetProjectAccessAsync(
            user.Id,
            projectId,
            allowed: true,
            createdAt: SqliteRepositorySeed.CreatedAt,
            cancellationToken: CancellationToken.None);
        await users.SetProjectGroupAccessAsync(
            user.Id,
            groupId,
            allowed: true,
            createdAt: SqliteRepositorySeed.CreatedAt,
            cancellationToken: CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);
        var projectGrants = await users.ListProjectAccessAsync(user.Id, CancellationToken.None);
        var groupGrants = await users.ListProjectGroupAccessAsync(user.Id, CancellationToken.None);

        await users.SetProjectAccessAsync(
            user.Id,
            projectId,
            allowed: false,
            createdAt: SqliteRepositorySeed.CreatedAt,
            cancellationToken: CancellationToken.None);
        await users.SetProjectGroupAccessAsync(
            user.Id,
            groupId,
            allowed: false,
            createdAt: SqliteRepositorySeed.CreatedAt,
            cancellationToken: CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);
        var revokedProjectGrants = await users.ListProjectAccessAsync(user.Id, CancellationToken.None);
        var revokedGroupGrants = await users.ListProjectGroupAccessAsync(user.Id, CancellationToken.None);

        Assert.Single(projectGrants);
        Assert.Single(groupGrants);
        Assert.Empty(revokedProjectGrants);
        Assert.Empty(revokedGroupGrants);
    }
}
