using Aeges.Core;

namespace Aeges.Core.Tests;

public sealed class RuntimeProjectRootTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 05, 12, 08, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Create_initializes_project_root()
    {
        var root = RuntimeProjectRoot.Create(
            new ProjectRootId("work"),
            "Work",
            "/work",
            CreatedAt);

        Assert.Equal(new ProjectRootId("work"), root.Id);
        Assert.Equal("Work", root.Name);
        Assert.Equal("/work", root.Path);
        Assert.Equal(CreatedAt, root.CreatedAt);
        Assert.Equal(CreatedAt, root.UpdatedAt);
        Assert.False(root.IsArchived);
    }

    [Fact]
    public void Archive_marks_project_root_archived()
    {
        var root = RuntimeProjectRoot.Create(new ProjectRootId("work"), "Work", "/work", CreatedAt);
        var archivedAt = CreatedAt.AddMinutes(5);

        root.Archive(archivedAt);

        Assert.True(root.IsArchived);
        Assert.Equal(archivedAt, root.ArchivedAt);
        Assert.Equal(archivedAt, root.UpdatedAt);
    }
}
