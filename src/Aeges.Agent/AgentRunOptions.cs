namespace Aeges.Agent;

/// <summary>
/// Configures one local agent runtime heartbeat.
/// </summary>
/// <param name="ConnectionString">The SQLite connection string used by the local runtime.</param>
/// <param name="MachineId">The durable machine identifier.</param>
/// <param name="MachineName">The human-readable machine name.</param>
/// <param name="Platform">The machine platform description.</param>
/// <param name="QueuedTaskPreviewLimit">The maximum number of queued tasks to include in the heartbeat snapshot.</param>
/// <param name="RunnerId">The runner assigned to newly created task iterations.</param>
/// <param name="RuntimeRootPath">The local runtime root path used for artifact and worktree path planning.</param>
/// <param name="RunnerTimeout">The maximum allowed runner execution time.</param>
/// <param name="ClaimQueuedTask">A value indicating whether one queued task should be claimed during this run.</param>
/// <param name="ExecuteRunner">A value indicating whether the prepared runner request should be executed.</param>
public sealed record AgentRunOptions(
    string ConnectionString,
    string MachineId,
    string MachineName,
    string Platform,
    int QueuedTaskPreviewLimit = 100,
    string RunnerId = "codex",
    string? RuntimeRootPath = null,
    TimeSpan? RunnerTimeout = null,
    bool ClaimQueuedTask = true,
    bool ExecuteRunner = false);
