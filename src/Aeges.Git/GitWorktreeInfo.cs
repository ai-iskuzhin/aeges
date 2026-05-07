namespace Aeges.Git;

/// <summary>
/// Describes an isolated Git worktree created for governed execution.
/// </summary>
/// <param name="RepositoryPath">The source repository path.</param>
/// <param name="WorktreePath">The created worktree path.</param>
/// <param name="BranchName">The worktree branch name.</param>
/// <param name="BaseCommit">The base commit checked out by the worktree, when known.</param>
public sealed record GitWorktreeInfo(
    string RepositoryPath,
    string WorktreePath,
    string BranchName,
    string? BaseCommit);
