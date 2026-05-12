using Aeges.Core;
using Aeges.Storage.Sqlite.Repositories;

namespace Aeges.Storage.Sqlite.Tests;

public sealed class SqliteProjectRepositoryTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 05, 07, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task Add_and_get_round_trips_project()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteProjectRepository(context);
        var groups = new SqliteProjectGroupRepository(context);
        await groups.AddAsync(
            RuntimeProjectGroup.Create(new ProjectGroupId("runtime"), "Runtime", CreatedAt, "/work"),
            CancellationToken.None);
        var project = RuntimeProject.Create(
            new ProjectId("project-001"),
            "Aeges",
            "/work/aeges",
            CreatedAt,
            new ProjectGroupId("runtime"));

        await repository.AddAsync(project, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(new ProjectId("project-001"), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(project.Id, stored.Id);
        Assert.Equal(project.Name, stored.Name);
        Assert.Equal(project.Path, stored.Path);
        Assert.Equal(project.GroupId, stored.GroupId);
        Assert.Equal(project.CreatedAt, stored.CreatedAt);
        Assert.Equal(project.UpdatedAt, stored.UpdatedAt);
        Assert.False(stored.IsArchived);
        Assert.Null(stored.ArchivedAt);
    }

    [Fact]
    public async Task List_returns_projects_ordered_by_name()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteProjectRepository(context);

        await repository.AddAsync(RuntimeProject.Create(new ProjectId("project-002"), "Zulu", "/work/zulu", CreatedAt), CancellationToken.None);
        await repository.AddAsync(RuntimeProject.Create(new ProjectId("project-001"), "Aeges", "/work/aeges", CreatedAt), CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var projects = await repository.ListAsync(CancellationToken.None);

        Assert.Collection(
            projects,
            project => Assert.Equal(new ProjectId("project-001"), project.Id),
            project => Assert.Equal(new ProjectId("project-002"), project.Id));
    }

    [Fact]
    public async Task Update_persists_project_metadata()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteProjectRepository(context);
        var project = RuntimeProject.Create(new ProjectId("project-001"), "Aeges", "/work/aeges", CreatedAt);
        await repository.AddAsync(project, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var updatedAt = CreatedAt.AddMinutes(5);
        project.Update("Aeges Runtime", "/work/aeges-runtime", updatedAt);
        await repository.UpdateAsync(project, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(new ProjectId("project-001"), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal("Aeges Runtime", stored.Name);
        Assert.Equal("/work/aeges-runtime", stored.Path);
        Assert.Equal(updatedAt, stored.UpdatedAt);
    }

    [Fact]
    public async Task Update_persists_project_group_assignment()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteProjectRepository(context);
        var groups = new SqliteProjectGroupRepository(context);
        await groups.AddAsync(
            RuntimeProjectGroup.Create(new ProjectGroupId("runtime"), "Runtime", CreatedAt, "/work/runtime"),
            CancellationToken.None);
        var project = RuntimeProject.Create(new ProjectId("project-001"), "Aeges", "/work/aeges", CreatedAt);
        await repository.AddAsync(project, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        project.AssignGroup(new ProjectGroupId("runtime"), CreatedAt.AddMinutes(5));
        await repository.UpdateAsync(project, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(new ProjectId("project-001"), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(new ProjectGroupId("runtime"), stored.GroupId);
    }

    [Fact]
    public async Task Update_persists_project_archive_state()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteProjectRepository(context);
        var project = RuntimeProject.Create(new ProjectId("project-001"), "Aeges", "/work/aeges", CreatedAt);
        await repository.AddAsync(project, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var archivedAt = CreatedAt.AddMinutes(5);
        project.Archive(archivedAt);
        await repository.UpdateAsync(project, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(new ProjectId("project-001"), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.True(stored.IsArchived);
        Assert.Equal(archivedAt, stored.ArchivedAt);
        Assert.Equal(archivedAt, stored.UpdatedAt);
    }

    [Fact]
    public async Task Get_returns_null_when_project_does_not_exist()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteProjectRepository(context);

        var stored = await repository.GetByIdAsync(new ProjectId("missing"), CancellationToken.None);

        Assert.Null(stored);
    }
}
