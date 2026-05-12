using Aeges.Core;

namespace Aeges.Core.Tests;

public sealed class RuntimeProjectGroupTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 05, 12, 08, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Create_initializes_project_group()
    {
        var group = RuntimeProjectGroup.Create(
            new ProjectGroupId("analitex"),
            "Analitex",
            CreatedAt,
            "/work/analitex");

        Assert.Equal(new ProjectGroupId("analitex"), group.Id);
        Assert.Equal("Analitex", group.Name);
        Assert.Equal("/work/analitex", group.Path);
        Assert.Equal(CreatedAt, group.CreatedAt);
        Assert.Equal(CreatedAt, group.UpdatedAt);
        Assert.False(group.IsArchived);
    }

    [Fact]
    public void Archive_marks_project_group_archived()
    {
        var group = RuntimeProjectGroup.Create(new ProjectGroupId("analitex"), "Analitex", CreatedAt);
        var archivedAt = CreatedAt.AddMinutes(5);

        group.Archive(archivedAt);

        Assert.True(group.IsArchived);
        Assert.Equal(archivedAt, group.ArchivedAt);
        Assert.Equal(archivedAt, group.UpdatedAt);
    }
}
