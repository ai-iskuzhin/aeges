namespace Aeges.Core;

/// <summary>
/// Defines the possible outcomes of a governance evaluation.
/// </summary>
public enum GovernanceDecisionStatus
{
    /// <summary>
    /// Execution may continue without an approval checkpoint.
    /// </summary>
    Allowed = 0,

    /// <summary>
    /// Execution must pause until an approval checkpoint is resolved.
    /// </summary>
    ApprovalRequired = 1,

    /// <summary>
    /// Execution must not continue because a hard governance boundary was crossed.
    /// </summary>
    Rejected = 2,
}
