namespace Aeges.Agent;

/// <summary>
/// Describes the observable result of one local agent runtime heartbeat.
/// </summary>
/// <param name="MachineId">The durable machine identifier.</param>
/// <param name="DatabasePath">The SQLite database path, when available.</param>
/// <param name="HeartbeatAt">The heartbeat timestamp.</param>
/// <param name="QueuedTaskCount">The number of queued tasks visible in the bounded preview.</param>
/// <param name="ClaimedTaskId">The task claimed by this run, when one was claimed.</param>
/// <param name="CreatedIterationId">The iteration created for the claimed task, when one was created.</param>
/// <param name="PromptArtifactId">The prompt artifact registered for the created iteration, when prepared.</param>
/// <param name="PromptPath">The prompt file path written for the created iteration, when prepared.</param>
/// <param name="WorktreePath">The planned worktree path for the created iteration, when prepared.</param>
/// <param name="ArtifactOutputDirectory">The artifact output directory for the created iteration, when prepared.</param>
/// <param name="RunnerExecutionId">The runner execution record created by this run, when execution was requested.</param>
/// <param name="RunnerStatus">The runner result status, when execution was requested.</param>
/// <param name="RunnerExitCode">The runner process exit code, when available.</param>
/// <param name="RunnerErrorSummary">The runner error summary, when execution did not succeed.</param>
/// <param name="WorktreeCreated">A value indicating whether a Git worktree was created for the iteration.</param>
/// <param name="WorktreeBaseCommit">The base commit used for the created worktree, when known.</param>
/// <param name="ClaimedTasks">The task-level work items claimed by this heartbeat.</param>
public sealed record AgentRunSnapshot(
    string MachineId,
    string? DatabasePath,
    DateTimeOffset HeartbeatAt,
    int QueuedTaskCount,
    string? ClaimedTaskId = null,
    string? CreatedIterationId = null,
    string? PromptArtifactId = null,
    string? PromptPath = null,
    string? WorktreePath = null,
    string? ArtifactOutputDirectory = null,
    string? RunnerExecutionId = null,
    string? RunnerStatus = null,
    int? RunnerExitCode = null,
    string? RunnerErrorSummary = null,
    bool WorktreeCreated = false,
    string? WorktreeBaseCommit = null,
    IReadOnlyList<AgentTaskRunSnapshot>? ClaimedTasks = null)
{
    /// <summary>
    /// Gets the task-level work items claimed by this heartbeat.
    /// </summary>
    public IReadOnlyList<AgentTaskRunSnapshot> ClaimedTasks { get; init; } =
        ClaimedTasks ?? Array.Empty<AgentTaskRunSnapshot>();
}
