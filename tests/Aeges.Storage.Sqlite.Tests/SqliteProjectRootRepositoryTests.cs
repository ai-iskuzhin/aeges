using Aeges.Core;
using Aeges.Storage.Sqlite.Repositories;

namespace Aeges.Storage.Sqlite.Tests;

public sealed class SqliteProjectRootRepositoryTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 05, 12, 08, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task Add_and_get_round_trips_project_root()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteProjectRootRepository(context);
        var root = RuntimeProjectRoot.Create(
            new ProjectRootId("work"),
            "Work",
            "/work",
            CreatedAt);

        await repository.AddAsync(root, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(new ProjectRootId("work"), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(root.Id, stored.Id);
        Assert.Equal(root.Name, stored.Name);
        Assert.Equal(root.Path, stored.Path);
        Assert.Equal(root.CreatedAt, stored.CreatedAt);
        Assert.Equal(root.UpdatedAt, stored.UpdatedAt);
    }

    [Fact]
    public async Task List_returns_project_roots_ordered_by_name()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteProjectRootRepository(context);

        await repository.AddAsync(RuntimeProjectRoot.Create(new ProjectRootId("zulu"), "Zulu", "/zulu", CreatedAt), CancellationToken.None);
        await repository.AddAsync(RuntimeProjectRoot.Create(new ProjectRootId("work"), "Work", "/work", CreatedAt), CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var roots = await repository.ListAsync(CancellationToken.None);

        Assert.Collection(
            roots,
            root => Assert.Equal(new ProjectRootId("work"), root.Id),
            root => Assert.Equal(new ProjectRootId("zulu"), root.Id));
    }

    [Fact]
    public async Task Update_persists_archive_state()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteProjectRootRepository(context);
        var root = RuntimeProjectRoot.Create(new ProjectRootId("work"), "Work", "/work", CreatedAt);
        await repository.AddAsync(root, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var archivedAt = CreatedAt.AddMinutes(5);
        root.Archive(archivedAt);
        await repository.UpdateAsync(root, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(new ProjectRootId("work"), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.True(stored.IsArchived);
        Assert.Equal(archivedAt, stored.ArchivedAt);
    }
}
