namespace Aeges.Core;

/// <summary>
/// Represents the result of a governance evaluation.
/// </summary>
public sealed class GovernanceDecision
{
    private GovernanceDecision(GovernanceDecisionStatus status, IReadOnlyList<string> reasons)
    {
        Status = status;
        Reasons = RequireReasons(status, reasons);
    }

    /// <summary>
    /// Gets the governance decision status.
    /// </summary>
    public GovernanceDecisionStatus Status { get; }

    /// <summary>
    /// Gets the human-readable reasons for the decision.
    /// </summary>
    public IReadOnlyList<string> Reasons { get; }

    /// <summary>
    /// Gets a value indicating whether execution may continue immediately.
    /// </summary>
    public bool IsAllowed => Status == GovernanceDecisionStatus.Allowed;

    /// <summary>
    /// Creates a decision that allows execution.
    /// </summary>
    /// <returns>An allowed decision.</returns>
    public static GovernanceDecision Allow() => new(GovernanceDecisionStatus.Allowed, []);

    /// <summary>
    /// Creates a decision that requires an approval checkpoint.
    /// </summary>
    /// <param name="reasons">The reasons approval is required.</param>
    /// <returns>An approval-required decision.</returns>
    public static GovernanceDecision RequireApproval(IEnumerable<string> reasons) =>
        new(GovernanceDecisionStatus.ApprovalRequired, reasons.ToArray());

    /// <summary>
    /// Creates a decision that rejects execution.
    /// </summary>
    /// <param name="reasons">The rejection reasons.</param>
    /// <returns>A rejected decision.</returns>
    public static GovernanceDecision Reject(IEnumerable<string> reasons) =>
        new(GovernanceDecisionStatus.Rejected, reasons.ToArray());

    private static IReadOnlyList<string> RequireReasons(
        GovernanceDecisionStatus status,
        IReadOnlyList<string> reasons)
    {
        if (status == GovernanceDecisionStatus.Allowed)
        {
            return [];
        }

        if (reasons.Count == 0 || reasons.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("A non-allowed governance decision must include non-empty reasons.", nameof(reasons));
        }

        return reasons.ToArray();
    }
}
