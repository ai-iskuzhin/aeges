using Aeges.Core;

namespace Aeges.Core.Tests;

public sealed class RuntimeProjectTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 05, 07, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Create_initializes_project()
    {
        var project = RuntimeProject.Create(
            new ProjectId("project-001"),
            "Aeges",
            "/work/aeges",
            CreatedAt);

        Assert.Equal(new ProjectId("project-001"), project.Id);
        Assert.Equal("Aeges", project.Name);
        Assert.Equal("/work/aeges", project.Path);
        Assert.Null(project.GroupId);
        Assert.Equal(CreatedAt, project.CreatedAt);
        Assert.Equal(CreatedAt, project.UpdatedAt);
        Assert.False(project.IsArchived);
        Assert.Null(project.ArchivedAt);
    }

    [Fact]
    public void Rehydrate_restores_project_state()
    {
        var updatedAt = CreatedAt.AddMinutes(5);

        var project = RuntimeProject.Rehydrate(
            new ProjectId("project-001"),
            "Aeges",
            "/work/aeges",
            CreatedAt,
            updatedAt);

        Assert.Equal(new ProjectId("project-001"), project.Id);
        Assert.Equal("Aeges", project.Name);
        Assert.Equal("/work/aeges", project.Path);
        Assert.Null(project.GroupId);
        Assert.Equal(CreatedAt, project.CreatedAt);
        Assert.Equal(updatedAt, project.UpdatedAt);
        Assert.False(project.IsArchived);
        Assert.Null(project.ArchivedAt);
    }

    [Fact]
    public void Rehydrate_restores_archive_state()
    {
        var archivedAt = CreatedAt.AddMinutes(10);

        var project = RuntimeProject.Rehydrate(
            new ProjectId("project-001"),
            "Aeges",
            "/work/aeges",
            CreatedAt,
            archivedAt,
            isArchived: true,
            archivedAt: archivedAt);

        Assert.True(project.IsArchived);
        Assert.Equal(archivedAt, project.ArchivedAt);
        Assert.Equal(archivedAt, project.UpdatedAt);
    }

    [Fact]
    public void Update_changes_metadata_and_timestamp()
    {
        var groupId = new ProjectGroupId("runtime");
        var project = RuntimeProject.Create(
            new ProjectId("project-001"),
            "Aeges",
            "/work/aeges",
            CreatedAt);
        var updatedAt = CreatedAt.AddMinutes(1);

        project.Update("Aeges Runtime", "/work/aeges-runtime", updatedAt, groupId);

        Assert.Equal("Aeges Runtime", project.Name);
        Assert.Equal("/work/aeges-runtime", project.Path);
        Assert.Equal(groupId, project.GroupId);
        Assert.Equal(updatedAt, project.UpdatedAt);
    }

    [Fact]
    public void AssignGroup_changes_project_group_and_timestamp()
    {
        var project = RuntimeProject.Create(
            new ProjectId("project-001"),
            "Aeges",
            "/work/aeges",
            CreatedAt,
            new ProjectGroupId("old-group"));
        var updatedAt = CreatedAt.AddMinutes(1);

        project.AssignGroup(new ProjectGroupId("new-group"), updatedAt);

        Assert.Equal(new ProjectGroupId("new-group"), project.GroupId);
        Assert.Equal(updatedAt, project.UpdatedAt);
    }

    [Fact]
    public void Archive_marks_project_archived_and_updates_timestamp()
    {
        var project = RuntimeProject.Create(
            new ProjectId("project-001"),
            "Aeges",
            "/work/aeges",
            CreatedAt);
        var archivedAt = CreatedAt.AddMinutes(2);

        project.Archive(archivedAt);

        Assert.True(project.IsArchived);
        Assert.Equal(archivedAt, project.ArchivedAt);
        Assert.Equal(archivedAt, project.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_rejects_empty_values(string value)
    {
        Assert.Throws<ArgumentException>(
            () => RuntimeProject.Create(new ProjectId("project-001"), value, "/work/aeges", CreatedAt));
        Assert.Throws<ArgumentException>(
            () => RuntimeProject.Create(new ProjectId("project-001"), "Aeges", value, CreatedAt));
    }
}
