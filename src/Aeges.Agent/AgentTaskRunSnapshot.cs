namespace Aeges.Agent;

/// <summary>
/// Describes the task-level work performed during one local agent runtime heartbeat.
/// </summary>
/// <param name="TaskId">The task claimed by this run.</param>
/// <param name="ProjectId">The project that owns the claimed task.</param>
/// <param name="CreatedIterationId">The iteration created for the claimed task.</param>
/// <param name="PromptArtifactId">The prompt artifact registered for the created iteration.</param>
/// <param name="PromptPath">The prompt file path written for the created iteration.</param>
/// <param name="WorktreePath">The planned worktree path for the created iteration.</param>
/// <param name="ArtifactOutputDirectory">The artifact output directory for the created iteration.</param>
/// <param name="RunnerExecutionId">The runner execution record created by this run, when execution was requested.</param>
/// <param name="RunnerStatus">The runner result status, when execution was requested.</param>
/// <param name="RunnerExitCode">The runner process exit code, when available.</param>
/// <param name="RunnerErrorSummary">The runner error summary, when execution did not succeed.</param>
/// <param name="WorktreeCreated">A value indicating whether a Git worktree was created for the iteration.</param>
/// <param name="WorktreeBaseCommit">The base commit used for the created worktree, when known.</param>
public sealed record AgentTaskRunSnapshot(
    string TaskId,
    string ProjectId,
    string CreatedIterationId,
    string PromptArtifactId,
    string PromptPath,
    string WorktreePath,
    string ArtifactOutputDirectory,
    string? RunnerExecutionId,
    string? RunnerStatus,
    int? RunnerExitCode,
    string? RunnerErrorSummary,
    bool WorktreeCreated,
    string? WorktreeBaseCommit);
