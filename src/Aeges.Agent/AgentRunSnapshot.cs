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
    string? ArtifactOutputDirectory = null);
