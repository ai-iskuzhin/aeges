using Aeges.Core;

namespace Aeges.Git;

/// <summary>
/// Defines Git operations required by governed runtime orchestration.
/// </summary>
public interface IGitRuntime
{
    /// <summary>
    /// Detects the Git repository containing a path.
    /// </summary>
    /// <param name="startPath">The path to inspect.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The repository information, or <see langword="null"/> when no repository is detected.</returns>
    Task<GitRepositoryInfo?> DetectRepositoryAsync(string startPath, CancellationToken cancellationToken);

    /// <summary>
    /// Creates an isolated worktree for governed execution.
    /// </summary>
    /// <param name="request">The worktree creation request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The created worktree information.</returns>
    Task<GitWorktreeInfo> CreateWorktreeAsync(GitWorktreeRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Captures repository status.
    /// </summary>
    /// <param name="repositoryPath">The repository path.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The status snapshot.</returns>
    Task<GitStatusSnapshot> GetStatusAsync(string repositoryPath, CancellationToken cancellationToken);

    /// <summary>
    /// Captures repository diff information.
    /// </summary>
    /// <param name="repositoryPath">The repository path.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The diff snapshot.</returns>
    Task<GitDiffSnapshot> GetDiffAsync(string repositoryPath, CancellationToken cancellationToken);

    /// <summary>
    /// Records the current base commit for a task or iteration.
    /// </summary>
    /// <param name="repositoryPath">The repository path.</param>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="iterationId">The iteration identifier, when iteration-scoped.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The base commit record.</returns>
    Task<GitBaseCommit> GetBaseCommitAsync(
        string repositoryPath,
        ProjectId projectId,
        TaskId taskId,
        IterationId? iterationId,
        CancellationToken cancellationToken);
}
