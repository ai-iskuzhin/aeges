namespace Aeges.Core;

/// <summary>
/// Defines the durable lifecycle states supported by a task iteration.
/// </summary>
public enum TaskIterationStatus
{
    /// <summary>
    /// The iteration has been created and is waiting for execution.
    /// </summary>
    Created = 0,

    /// <summary>
    /// A runner is executing the iteration.
    /// </summary>
    Running = 1,

    /// <summary>
    /// The runtime is reviewing iteration artifacts.
    /// </summary>
    Reviewing = 2,

    /// <summary>
    /// The iteration is paused until approval is resolved.
    /// </summary>
    WaitingApproval = 3,

    /// <summary>
    /// The iteration completed successfully.
    /// </summary>
    Completed = 4,

    /// <summary>
    /// The iteration failed.
    /// </summary>
    Failed = 5,

    /// <summary>
    /// The iteration was cancelled.
    /// </summary>
    Cancelled = 6,
}

/// <summary>
/// Provides conversion and classification helpers for <see cref="TaskIterationStatus"/>.
/// </summary>
public static class TaskIterationStatusExtensions
{
    /// <summary>
    /// Converts an iteration status into its stable storage representation.
    /// </summary>
    /// <param name="status">The status to convert.</param>
    /// <returns>The lowercase storage value for the status.</returns>
    public static string ToStorageValue(this TaskIterationStatus status) => status switch
    {
        TaskIterationStatus.Created => "created",
        TaskIterationStatus.Running => "running",
        TaskIterationStatus.Reviewing => "reviewing",
        TaskIterationStatus.WaitingApproval => "waiting_approval",
        TaskIterationStatus.Completed => "completed",
        TaskIterationStatus.Failed => "failed",
        TaskIterationStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown iteration status."),
    };

    /// <summary>
    /// Parses a stable storage value into an iteration status.
    /// </summary>
    /// <param name="value">The persisted status value.</param>
    /// <returns>The matching iteration status.</returns>
    public static TaskIterationStatus FromStorageValue(string value) => value switch
    {
        "created" => TaskIterationStatus.Created,
        "running" => TaskIterationStatus.Running,
        "reviewing" => TaskIterationStatus.Reviewing,
        "waiting_approval" => TaskIterationStatus.WaitingApproval,
        "completed" => TaskIterationStatus.Completed,
        "failed" => TaskIterationStatus.Failed,
        "cancelled" => TaskIterationStatus.Cancelled,
        _ => throw new ArgumentException($"Unknown iteration status '{value}'.", nameof(value)),
    };

    /// <summary>
    /// Determines whether an iteration status ends the iteration lifecycle.
    /// </summary>
    /// <param name="status">The status to classify.</param>
    /// <returns><see langword="true"/> when the status is terminal; otherwise <see langword="false"/>.</returns>
    public static bool IsTerminal(this TaskIterationStatus status) =>
        status is TaskIterationStatus.Completed or TaskIterationStatus.Failed or TaskIterationStatus.Cancelled;
}
