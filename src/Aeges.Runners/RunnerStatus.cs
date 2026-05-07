namespace Aeges.Runners;

/// <summary>
/// Defines the possible outcomes of a governed runner execution.
/// </summary>
public enum RunnerStatus
{
    /// <summary>
    /// The runner completed successfully.
    /// </summary>
    Succeeded = 0,

    /// <summary>
    /// The runner completed with a failure.
    /// </summary>
    Failed = 1,

    /// <summary>
    /// The runner exceeded its configured timeout.
    /// </summary>
    TimedOut = 2,

    /// <summary>
    /// The runner was cancelled before completion.
    /// </summary>
    Cancelled = 3,

    /// <summary>
    /// The runner produced an approval request instead of continuing execution.
    /// </summary>
    ApprovalRequired = 4,
}

/// <summary>
/// Provides conversion helpers for <see cref="RunnerStatus"/>.
/// </summary>
public static class RunnerStatusExtensions
{
    /// <summary>
    /// Converts a runner status into its stable storage representation.
    /// </summary>
    /// <param name="status">The status to convert.</param>
    /// <returns>The lowercase storage value for the runner status.</returns>
    public static string ToStorageValue(this RunnerStatus status) => status switch
    {
        RunnerStatus.Succeeded => "succeeded",
        RunnerStatus.Failed => "failed",
        RunnerStatus.TimedOut => "timed_out",
        RunnerStatus.Cancelled => "cancelled",
        RunnerStatus.ApprovalRequired => "approval_required",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown runner status."),
    };

    /// <summary>
    /// Parses a stable storage value into a runner status.
    /// </summary>
    /// <param name="value">The persisted runner status value.</param>
    /// <returns>The matching runner status.</returns>
    public static RunnerStatus FromStorageValue(string value) => value switch
    {
        "succeeded" => RunnerStatus.Succeeded,
        "failed" => RunnerStatus.Failed,
        "timed_out" => RunnerStatus.TimedOut,
        "cancelled" => RunnerStatus.Cancelled,
        "approval_required" => RunnerStatus.ApprovalRequired,
        _ => throw new ArgumentException($"Unknown runner status '{value}'.", nameof(value)),
    };

    /// <summary>
    /// Determines whether the status represents an interrupted execution.
    /// </summary>
    /// <param name="status">The status to classify.</param>
    /// <returns><see langword="true"/> for timeout or cancellation; otherwise <see langword="false"/>.</returns>
    public static bool IsInterrupted(this RunnerStatus status) =>
        status is RunnerStatus.TimedOut or RunnerStatus.Cancelled;
}
