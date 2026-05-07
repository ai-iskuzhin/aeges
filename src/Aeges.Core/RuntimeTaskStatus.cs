namespace Aeges.Core;

/// <summary>
/// Defines the durable lifecycle states supported by an Aeges runtime task.
/// </summary>
public enum RuntimeTaskStatus
{
    /// <summary>
    /// The task has been created and is waiting for runtime scheduling.
    /// </summary>
    Queued = 0,

    /// <summary>
    /// The runtime is preparing the task plan or execution setup.
    /// </summary>
    Planning = 1,

    /// <summary>
    /// A runner is executing the task inside governed constraints.
    /// </summary>
    Running = 2,

    /// <summary>
    /// The runtime or a reviewer is evaluating produced artifacts.
    /// </summary>
    Reviewing = 3,

    /// <summary>
    /// The task is paused until a required approval is resolved.
    /// </summary>
    WaitingApproval = 4,

    /// <summary>
    /// The task finished successfully.
    /// </summary>
    Completed = 5,

    /// <summary>
    /// The task ended with a recorded failure.
    /// </summary>
    Failed = 6,

    /// <summary>
    /// The task was cancelled before successful completion.
    /// </summary>
    Cancelled = 7,
}

/// <summary>
/// Provides conversion and classification helpers for <see cref="RuntimeTaskStatus"/>.
/// </summary>
public static class RuntimeTaskStatusExtensions
{
    /// <summary>
    /// Converts a runtime task status into its stable storage representation.
    /// </summary>
    /// <param name="status">The status to convert.</param>
    /// <returns>The lowercase storage value for the status.</returns>
    public static string ToStorageValue(this RuntimeTaskStatus status) => status switch
    {
        RuntimeTaskStatus.Queued => "queued",
        RuntimeTaskStatus.Planning => "planning",
        RuntimeTaskStatus.Running => "running",
        RuntimeTaskStatus.Reviewing => "reviewing",
        RuntimeTaskStatus.WaitingApproval => "waiting_approval",
        RuntimeTaskStatus.Completed => "completed",
        RuntimeTaskStatus.Failed => "failed",
        RuntimeTaskStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown task status."),
    };

    /// <summary>
    /// Parses a stable storage value into a runtime task status.
    /// </summary>
    /// <param name="value">The persisted status value.</param>
    /// <returns>The matching runtime task status.</returns>
    public static RuntimeTaskStatus FromStorageValue(string value) => value switch
    {
        "queued" => RuntimeTaskStatus.Queued,
        "planning" => RuntimeTaskStatus.Planning,
        "running" => RuntimeTaskStatus.Running,
        "reviewing" => RuntimeTaskStatus.Reviewing,
        "waiting_approval" => RuntimeTaskStatus.WaitingApproval,
        "completed" => RuntimeTaskStatus.Completed,
        "failed" => RuntimeTaskStatus.Failed,
        "cancelled" => RuntimeTaskStatus.Cancelled,
        _ => throw new ArgumentException($"Unknown task status '{value}'.", nameof(value)),
    };

    /// <summary>
    /// Determines whether a task status ends the task lifecycle.
    /// </summary>
    /// <param name="status">The status to classify.</param>
    /// <returns><see langword="true"/> when the status is terminal; otherwise <see langword="false"/>.</returns>
    public static bool IsTerminal(this RuntimeTaskStatus status) =>
        status is RuntimeTaskStatus.Completed or RuntimeTaskStatus.Failed or RuntimeTaskStatus.Cancelled;
}
