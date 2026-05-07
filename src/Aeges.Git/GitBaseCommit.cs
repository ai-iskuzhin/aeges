using Aeges.Core;

namespace Aeges.Git;

/// <summary>
/// Records the base commit used for governed task execution.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="TaskId">The task identifier.</param>
/// <param name="IterationId">The iteration identifier, when the base commit is iteration-scoped.</param>
/// <param name="CommitSha">The base commit SHA.</param>
/// <param name="BranchName">The source branch name, when known.</param>
public sealed record GitBaseCommit(
    ProjectId ProjectId,
    TaskId TaskId,
    IterationId? IterationId,
    string CommitSha,
    string? BranchName);
