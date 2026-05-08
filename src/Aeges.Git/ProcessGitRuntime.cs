using System.Diagnostics;
using Aeges.Core;

namespace Aeges.Git;

/// <summary>
/// Process-backed implementation of <see cref="IGitRuntime"/> using the local Git executable.
/// </summary>
public sealed class ProcessGitRuntime : IGitRuntime
{
    private readonly string executable;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessGitRuntime"/> class.
    /// </summary>
    /// <param name="executable">The Git executable name or path.</param>
    /// <param name="timeProvider">The time provider used for captured snapshots.</param>
    public ProcessGitRuntime(string executable = "git", TimeProvider? timeProvider = null)
    {
        this.executable = RequireText(executable, nameof(executable));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<GitRepositoryInfo?> DetectRepositoryAsync(
        string startPath,
        CancellationToken cancellationToken)
    {
        var requestedPath = RequireText(startPath, nameof(startPath));
        var topLevel = await RunGitOrNullAsync(requestedPath, ["rev-parse", "--show-toplevel"], cancellationToken);

        if (topLevel is null)
        {
            return null;
        }

        var rootPath = topLevel.Trim();
        var gitDirectory = await RunGitAsync(rootPath, ["rev-parse", "--git-dir"], cancellationToken);
        var branch = await RunGitOrNullAsync(rootPath, ["branch", "--show-current"], cancellationToken);
        var head = await RunGitOrNullAsync(rootPath, ["rev-parse", "HEAD"], cancellationToken);

        return new GitRepositoryInfo(
            rootPath,
            NormalizeGitDirectory(rootPath, gitDirectory.Trim()),
            NormalizeOptional(branch),
            NormalizeOptional(head));
    }

    /// <inheritdoc />
    public async Task<GitWorktreeInfo> CreateWorktreeAsync(
        GitWorktreeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var repository = await DetectRepositoryAsync(request.RepositoryPath, cancellationToken)
            ?? throw new InvalidOperationException($"Repository '{request.RepositoryPath}' was not found.");
        var worktreePath = request.IterationId is null
            ? GitWorktreePathBuilder.BuildTaskPath(request.WorktreeRootPath, request.ProjectId, request.TaskId)
            : GitWorktreePathBuilder.BuildIterationPath(
                request.WorktreeRootPath,
                request.ProjectId,
                request.TaskId,
                request.IterationId.Value);
        var parentDirectory = Path.GetDirectoryName(worktreePath);

        if (!string.IsNullOrWhiteSpace(parentDirectory))
        {
            Directory.CreateDirectory(parentDirectory);
        }

        var baseCommit = NormalizeOptional(request.BaseCommit) ?? repository.HeadCommit ?? "HEAD";
        await RunGitAsync(
            repository.RootPath,
            ["worktree", "add", "-b", RequireText(request.BranchName, nameof(request.BranchName)), worktreePath, baseCommit],
            cancellationToken);

        return new GitWorktreeInfo(repository.RootPath, worktreePath, request.BranchName, baseCommit);
    }

    /// <inheritdoc />
    public async Task<GitStatusSnapshot> GetStatusAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var repository = await DetectRepositoryAsync(repositoryPath, cancellationToken)
            ?? throw new InvalidOperationException($"Repository '{repositoryPath}' was not found.");
        var output = await RunGitAsync(repository.RootPath, ["status", "--porcelain=v1"], cancellationToken);
        var entries = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseStatusEntry)
            .ToArray();

        return new GitStatusSnapshot(repository.RootPath, entries, timeProvider.GetUtcNow());
    }

    /// <inheritdoc />
    public async Task<GitDiffSnapshot> GetDiffAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var repository = await DetectRepositoryAsync(repositoryPath, cancellationToken)
            ?? throw new InvalidOperationException($"Repository '{repositoryPath}' was not found.");
        var diff = await RunGitAsync(repository.RootPath, ["diff", "--no-ext-diff"], cancellationToken);
        var changedPaths = await RunGitAsync(repository.RootPath, ["diff", "--name-only"], cancellationToken);

        return new GitDiffSnapshot(
            repository.RootPath,
            diff,
            changedPaths.Split('\n', StringSplitOptions.RemoveEmptyEntries),
            timeProvider.GetUtcNow());
    }

    /// <inheritdoc />
    public async Task<GitBaseCommit> GetBaseCommitAsync(
        string repositoryPath,
        ProjectId projectId,
        TaskId taskId,
        IterationId? iterationId,
        CancellationToken cancellationToken)
    {
        var repository = await DetectRepositoryAsync(repositoryPath, cancellationToken)
            ?? throw new InvalidOperationException($"Repository '{repositoryPath}' was not found.");
        var commit = await RunGitAsync(repository.RootPath, ["rev-parse", "HEAD"], cancellationToken);

        return new GitBaseCommit(
            projectId,
            taskId,
            iterationId,
            commit.Trim(),
            repository.CurrentBranch);
    }

    private async Task<string?> RunGitOrNullAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await RunProcessAsync(workingDirectory, arguments, cancellationToken);

        return result.ExitCode == 0 ? result.StandardOutput : null;
    }

    private async Task<string> RunGitAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await RunProcessAsync(workingDirectory, arguments, cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git command failed with exit code {result.ExitCode}: {result.StandardError.Trim()}");
        }

        return result.StandardOutput;
    }

    private async Task<GitProcessResult> RunProcessAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = RequireExistingDirectory(workingDirectory),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Git process failed to start.");
            }

            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            return new GitProcessResult(
                process.ExitCode,
                await stdout,
                await stderr);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            KillProcess(process);
            throw;
        }
    }

    private static GitStatusEntry ParseStatusEntry(string line)
    {
        if (line.Length < 4)
        {
            throw new InvalidOperationException($"Unexpected Git status line '{line}'.");
        }

        return new GitStatusEntry(line[3..], line[..2].Trim());
    }

    private static string NormalizeGitDirectory(string rootPath, string gitDirectory) =>
        Path.IsPathFullyQualified(gitDirectory)
            ? gitDirectory
            : Path.GetFullPath(Path.Combine(rootPath, gitDirectory));

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value;
    }

    private static string RequireExistingDirectory(string path)
    {
        var fullPath = Path.GetFullPath(RequireText(path, nameof(path)));

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Directory '{fullPath}' was not found.");
        }

        return fullPath;
    }

    private static void KillProcess(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        process.Kill(entireProcessTree: true);
    }

    private sealed record GitProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
