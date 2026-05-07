namespace Aeges.Core;

/// <summary>
/// Defines the durable lifecycle states supported by an approval request.
/// </summary>
public enum ApprovalStatus
{
    /// <summary>
    /// The approval request is waiting for a decision.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The requested action was approved.
    /// </summary>
    Approved = 1,

    /// <summary>
    /// The requested action was rejected.
    /// </summary>
    Rejected = 2,

    /// <summary>
    /// The approval request was cancelled before a decision.
    /// </summary>
    Cancelled = 3,
}

/// <summary>
/// Provides conversion and classification helpers for <see cref="ApprovalStatus"/>.
/// </summary>
public static class ApprovalStatusExtensions
{
    /// <summary>
    /// Converts an approval status into its stable storage representation.
    /// </summary>
    /// <param name="status">The status to convert.</param>
    /// <returns>The lowercase storage value for the status.</returns>
    public static string ToStorageValue(this ApprovalStatus status) => status switch
    {
        ApprovalStatus.Pending => "pending",
        ApprovalStatus.Approved => "approved",
        ApprovalStatus.Rejected => "rejected",
        ApprovalStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown approval status."),
    };

    /// <summary>
    /// Parses a stable storage value into an approval status.
    /// </summary>
    /// <param name="value">The persisted status value.</param>
    /// <returns>The matching approval status.</returns>
    public static ApprovalStatus FromStorageValue(string value) => value switch
    {
        "pending" => ApprovalStatus.Pending,
        "approved" => ApprovalStatus.Approved,
        "rejected" => ApprovalStatus.Rejected,
        "cancelled" => ApprovalStatus.Cancelled,
        _ => throw new ArgumentException($"Unknown approval status '{value}'.", nameof(value)),
    };

    /// <summary>
    /// Determines whether an approval status ends the approval lifecycle.
    /// </summary>
    /// <param name="status">The status to classify.</param>
    /// <returns><see langword="true"/> when the status is terminal; otherwise <see langword="false"/>.</returns>
    public static bool IsTerminal(this ApprovalStatus status) =>
        status is ApprovalStatus.Approved or ApprovalStatus.Rejected or ApprovalStatus.Cancelled;
}
