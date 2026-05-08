using Aeges.Core;
using Aeges.Git;

namespace Aeges.Git.Tests;

public sealed class ProcessGitRuntimeTests
{
    [Fact]
    public async Task DetectRepositoryAsync_returns_repository_metadata()
    {
        using var repository = await TemporaryGitRepository.CreateAsync();
        var runtime = new ProcessGitRuntime();

        var info = await runtime.DetectRepositoryAsync(repository.Path, CancellationToken.None);

        Assert.NotNull(info);
        Assert.Equal("repo", System.IO.Path.GetFileName(info.RootPath));
        Assert.Equal("main", info.CurrentBranch);
        Assert.NotNull(info.HeadCommit);
        Assert.True(Directory.Exists(info.GitDirectoryPath));
    }

    [Fact]
    public async Task GetStatusAsync_and_GetDiffAsync_capture_working_tree_changes()
    {
        using var repository = await TemporaryGitRepository.CreateAsync();
        var runtime = new ProcessGitRuntime();
        await File.WriteAllTextAsync(System.IO.Path.Combine(repository.Path, "file.txt"), "changed\n");

        var status = await runtime.GetStatusAsync(repository.Path, CancellationToken.None);
        var diff = await runtime.GetDiffAsync(repository.Path, CancellationToken.None);

        Assert.False(status.IsClean);
        Assert.Contains(status.Entries, entry => entry.Path == "file.txt" && entry.StatusCode == "M");
        Assert.Contains("changed", diff.DiffText);
        Assert.Contains("file.txt", diff.ChangedPaths);
    }

    [Fact]
    public async Task GetBaseCommitAsync_returns_head_commit_and_branch()
    {
        using var repository = await TemporaryGitRepository.CreateAsync();
        var runtime = new ProcessGitRuntime();

        var baseCommit = await runtime.GetBaseCommitAsync(
            repository.Path,
            new ProjectId("project-001"),
            new TaskId("task-001"),
            new IterationId("iteration-001"),
            CancellationToken.None);

        Assert.Equal(new ProjectId("project-001"), baseCommit.ProjectId);
        Assert.Equal(new TaskId("task-001"), baseCommit.TaskId);
        Assert.Equal(new IterationId("iteration-001"), baseCommit.IterationId);
        Assert.Equal(repository.HeadCommit, baseCommit.CommitSha);
        Assert.Equal("main", baseCommit.BranchName);
    }

    [Fact]
    public async Task CreateWorktreeAsync_creates_iteration_worktree_at_deterministic_path()
    {
        using var repository = await TemporaryGitRepository.CreateAsync();
        var runtime = new ProcessGitRuntime();
        var worktreeRoot = System.IO.Path.Combine(repository.RootPath, "worktrees");

        var worktree = await runtime.CreateWorktreeAsync(
            new GitWorktreeRequest(
                repository.Path,
                worktreeRoot,
                new ProjectId("project-001"),
                new TaskId("task-001"),
                new IterationId("iteration-001"),
                "aeges/task-001/iteration-001",
                repository.HeadCommit),
            CancellationToken.None);

        Assert.Equal("repo", System.IO.Path.GetFileName(worktree.RepositoryPath));
        Assert.Equal(
            System.IO.Path.Combine(worktreeRoot, "project-001", "task-001", "iteration-001"),
            worktree.WorktreePath);
        Assert.True(File.Exists(System.IO.Path.Combine(worktree.WorktreePath, "file.txt")));
        Assert.Equal(repository.HeadCommit, worktree.BaseCommit);
    }

    private sealed class TemporaryGitRepository : IDisposable
    {
        private TemporaryGitRepository(string rootPath, string repositoryPath, string headCommit)
        {
            RootPath = rootPath;
            Path = repositoryPath;
            HeadCommit = headCommit;
        }

        public string RootPath { get; }

        public string Path { get; }

        public string HeadCommit { get; }

        public static async Task<TemporaryGitRepository> CreateAsync()
        {
            var rootPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"aeges-git-{Guid.NewGuid():N}");
            var repositoryPath = System.IO.Path.Combine(rootPath, "repo");
            Directory.CreateDirectory(repositoryPath);

            await RunGitAsync(repositoryPath, ["init", "-b", "main"]);
            await RunGitAsync(repositoryPath, ["config", "user.email", "aeges@example.test"]);
            await RunGitAsync(repositoryPath, ["config", "user.name", "Aeges Tests"]);
            await File.WriteAllTextAsync(System.IO.Path.Combine(repositoryPath, "file.txt"), "initial\n");
            await RunGitAsync(repositoryPath, ["add", "file.txt"]);
            await RunGitAsync(repositoryPath, ["commit", "-m", "Initial commit"]);
            var headCommit = (await RunGitAsync(repositoryPath, ["rev-parse", "HEAD"])).Trim();

            return new TemporaryGitRepository(rootPath, repositoryPath, headCommit);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }

        private static async Task<string> RunGitAsync(string workingDirectory, IReadOnlyList<string> arguments)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo("git")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("Git process failed to start.");
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Git failed with exit code {process.ExitCode}: {stderr}");
            }

            return stdout;
        }
    }
}
