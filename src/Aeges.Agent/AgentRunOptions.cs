namespace Aeges.Agent;

/// <summary>
/// Configures one local agent runtime heartbeat.
/// </summary>
/// <param name="ConnectionString">The SQLite connection string used by the local runtime.</param>
/// <param name="MachineId">The durable machine identifier.</param>
/// <param name="MachineName">The human-readable machine name.</param>
/// <param name="Platform">The machine platform description.</param>
/// <param name="QueuedTaskPreviewLimit">The maximum number of queued tasks to include in the heartbeat snapshot.</param>
public sealed record AgentRunOptions(
    string ConnectionString,
    string MachineId,
    string MachineName,
    string Platform,
    int QueuedTaskPreviewLimit = 100);
