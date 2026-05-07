using Aeges.Core;

namespace Aeges.Git;

/// <summary>
/// Describes a request to create an isolated Git worktree.
/// </summary>
/// <param name="RepositoryPath">The source repository path.</param>
/// <param name="WorktreeRootPath">The root directory where Aeges worktrees are stored.</param>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="TaskId">The task identifier.</param>
/// <param name="IterationId">The iteration identifier, when the worktree is iteration-scoped.</param>
/// <param name="BranchName">The worktree branch name.</param>
/// <param name="BaseCommit">The base commit to check out, when known.</param>
public sealed record GitWorktreeRequest(
    string RepositoryPath,
    string WorktreeRootPath,
    ProjectId ProjectId,
    TaskId TaskId,
    IterationId? IterationId,
    string BranchName,
    string? BaseCommit = null);
