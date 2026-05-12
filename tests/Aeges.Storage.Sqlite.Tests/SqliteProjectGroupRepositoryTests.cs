using Aeges.Core;
using Aeges.Storage.Sqlite.Repositories;

namespace Aeges.Storage.Sqlite.Tests;

public sealed class SqliteProjectGroupRepositoryTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 05, 12, 08, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task Add_and_get_round_trips_project_group()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteProjectGroupRepository(context);
        var group = RuntimeProjectGroup.Create(
            new ProjectGroupId("analitex"),
            "Analitex",
            CreatedAt,
            "/work/analitex");

        await repository.AddAsync(group, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(new ProjectGroupId("analitex"), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(group.Id, stored.Id);
        Assert.Equal(group.Name, stored.Name);
        Assert.Equal(group.Path, stored.Path);
        Assert.Equal(group.CreatedAt, stored.CreatedAt);
        Assert.Equal(group.UpdatedAt, stored.UpdatedAt);
    }

    [Fact]
    public async Task List_returns_project_groups_ordered_by_name()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteProjectGroupRepository(context);

        await repository.AddAsync(RuntimeProjectGroup.Create(new ProjectGroupId("zulu"), "Zulu", CreatedAt), CancellationToken.None);
        await repository.AddAsync(RuntimeProjectGroup.Create(new ProjectGroupId("analitex"), "Analitex", CreatedAt), CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var groups = await repository.ListAsync(CancellationToken.None);

        Assert.Collection(
            groups,
            group => Assert.Equal(new ProjectGroupId("analitex"), group.Id),
            group => Assert.Equal(new ProjectGroupId("zulu"), group.Id));
    }

    [Fact]
    public async Task Update_persists_archive_state()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteProjectGroupRepository(context);
        var group = RuntimeProjectGroup.Create(new ProjectGroupId("analitex"), "Analitex", CreatedAt);
        await repository.AddAsync(group, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var archivedAt = CreatedAt.AddMinutes(5);
        group.Archive(archivedAt);
        await repository.UpdateAsync(group, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(new ProjectGroupId("analitex"), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.True(stored.IsArchived);
        Assert.Equal(archivedAt, stored.ArchivedAt);
    }
}
