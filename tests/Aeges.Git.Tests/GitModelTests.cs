using Aeges.Core;
using Aeges.Git;

namespace Aeges.Git.Tests;

public sealed class GitModelTests
{
    private static readonly DateTimeOffset CapturedAt = new(2026, 05, 07, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Repository_info_records_detected_repository_metadata()
    {
        var info = new GitRepositoryInfo("/work/aeges", "/work/aeges/.git", "main", "abc123");

        Assert.Equal("/work/aeges", info.RootPath);
        Assert.Equal("/work/aeges/.git", info.GitDirectoryPath);
        Assert.Equal("main", info.CurrentBranch);
        Assert.Equal("abc123", info.HeadCommit);
    }

    [Fact]
    public void Base_commit_records_task_scope()
    {
        var baseCommit = new GitBaseCommit(
            new ProjectId("project-001"),
            new TaskId("task-001"),
            new IterationId("iteration-001"),
            "abc123",
            "main");

        Assert.Equal(new ProjectId("project-001"), baseCommit.ProjectId);
        Assert.Equal(new TaskId("task-001"), baseCommit.TaskId);
        Assert.Equal(new IterationId("iteration-001"), baseCommit.IterationId);
        Assert.Equal("abc123", baseCommit.CommitSha);
        Assert.Equal("main", baseCommit.BranchName);
    }

    [Fact]
    public void Status_snapshot_reports_clean_state_from_empty_entries()
    {
        var clean = new GitStatusSnapshot("/work/aeges", [], CapturedAt);
        var dirty = new GitStatusSnapshot(
            "/work/aeges",
            [new GitStatusEntry("src/Aeges.Core/RuntimeTask.cs", "M")],
            CapturedAt);

        Assert.True(clean.IsClean);
        Assert.False(dirty.IsClean);
    }

    [Fact]
    public void Diff_snapshot_records_changed_paths()
    {
        var diff = new GitDiffSnapshot(
            "/work/aeges",
            "diff --git a/file b/file",
            ["src/Aeges.Core/RuntimeTask.cs"],
            CapturedAt);

        Assert.Equal("/work/aeges", diff.RepositoryPath);
        Assert.Equal("diff --git a/file b/file", diff.DiffText);
        Assert.Equal("src/Aeges.Core/RuntimeTask.cs", diff.ChangedPaths[0]);
    }
}
