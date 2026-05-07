using Aeges.Core;
using Aeges.Git;

namespace Aeges.Git.Tests;

public sealed class GitWorktreePathBuilderTests
{
    [Fact]
    public void BuildTaskPath_places_task_worktree_under_project_directory()
    {
        var root = CreateRoot();

        var path = GitWorktreePathBuilder.BuildTaskPath(
            root,
            new ProjectId("project-001"),
            new TaskId("task-001"));

        Assert.Equal(
            Path.Combine(root, "project-001", "task-001"),
            path);
    }

    [Fact]
    public void BuildIterationPath_places_iteration_worktree_under_task_directory()
    {
        var root = CreateRoot();

        var path = GitWorktreePathBuilder.BuildIterationPath(
            root,
            new ProjectId("project-001"),
            new TaskId("task-001"),
            new IterationId("iteration-001"));

        Assert.Equal(
            Path.Combine(root, "project-001", "task-001", "iteration-001"),
            path);
    }

    [Fact]
    public void BuildPath_sanitizes_identifier_segments()
    {
        var root = CreateRoot();

        var path = GitWorktreePathBuilder.BuildTaskPath(
            root,
            new ProjectId("project/001"),
            new TaskId("task:001"));

        Assert.Equal(Path.Combine(root, "project_001", "task_001"), path);
    }

    [Fact]
    public void BuildPath_rejects_relative_worktree_root()
    {
        Assert.Throws<ArgumentException>(
            () => GitWorktreePathBuilder.BuildTaskPath(
                "relative/root",
                new ProjectId("project-001"),
                new TaskId("task-001")));
    }

    [Fact]
    public void RequireInsideRoot_accepts_paths_inside_root()
    {
        var root = CreateRoot();
        var candidate = Path.Combine(root, "project-001", "task-001");

        var normalized = GitWorktreePathBuilder.RequireInsideRoot(root, candidate);

        Assert.Equal(candidate, normalized);
    }

    [Fact]
    public void RequireInsideRoot_rejects_paths_that_escape_root()
    {
        var root = CreateRoot();
        var candidate = Path.Combine(root, "..", "outside");

        Assert.Throws<ArgumentException>(() => GitWorktreePathBuilder.RequireInsideRoot(root, candidate));
    }

    private static string CreateRoot() =>
        Path.Combine(Path.GetTempPath(), "aeges-tests", "worktrees");
}
