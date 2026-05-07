using Aeges.Core;

namespace Aeges.Application.Approvals;

/// <summary>
/// Describes a request to create a governed approval gate for a task.
/// </summary>
/// <param name="TaskId">The task that requires approval.</param>
/// <param name="IterationId">The related iteration, when applicable.</param>
/// <param name="Reason">The reason approval is required.</param>
/// <param name="RequestedAction">The action being requested.</param>
/// <param name="ApprovalId">The optional approval identifier. A new identifier is generated when omitted.</param>
public sealed record RequestApprovalRequest(
    TaskId TaskId,
    IterationId? IterationId,
    string Reason,
    string RequestedAction,
    ApprovalId? ApprovalId = null);
