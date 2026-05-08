namespace Aeges.Agent;

/// <summary>
/// Describes the observable result of one local agent runtime heartbeat.
/// </summary>
/// <param name="MachineId">The durable machine identifier.</param>
/// <param name="DatabasePath">The SQLite database path, when available.</param>
/// <param name="HeartbeatAt">The heartbeat timestamp.</param>
/// <param name="QueuedTaskCount">The number of queued tasks visible in the bounded preview.</param>
public sealed record AgentRunSnapshot(
    string MachineId,
    string? DatabasePath,
    DateTimeOffset HeartbeatAt,
    int QueuedTaskCount);
