namespace Aeges.Git;

/// <summary>
/// Describes a detected Git repository.
/// </summary>
/// <param name="RootPath">The repository root path.</param>
/// <param name="GitDirectoryPath">The repository Git directory path.</param>
/// <param name="CurrentBranch">The current branch name, when known.</param>
/// <param name="HeadCommit">The current HEAD commit, when known.</param>
public sealed record GitRepositoryInfo(
    string RootPath,
    string GitDirectoryPath,
    string? CurrentBranch,
    string? HeadCommit);
