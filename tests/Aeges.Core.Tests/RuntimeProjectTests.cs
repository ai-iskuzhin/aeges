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
        Assert.Equal(CreatedAt, project.CreatedAt);
        Assert.Equal(CreatedAt, project.UpdatedAt);
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
        Assert.Equal(CreatedAt, project.CreatedAt);
        Assert.Equal(updatedAt, project.UpdatedAt);
    }

    [Fact]
    public void Update_changes_metadata_and_timestamp()
    {
        var project = RuntimeProject.Create(
            new ProjectId("project-001"),
            "Aeges",
            "/work/aeges",
            CreatedAt);
        var updatedAt = CreatedAt.AddMinutes(1);

        project.Update("Aeges Runtime", "/work/aeges-runtime", updatedAt);

        Assert.Equal("Aeges Runtime", project.Name);
        Assert.Equal("/work/aeges-runtime", project.Path);
        Assert.Equal(updatedAt, project.UpdatedAt);
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
