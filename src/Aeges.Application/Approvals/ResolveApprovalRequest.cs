using Aeges.Core;

namespace Aeges.Application.Approvals;

/// <summary>
/// Describes a request to resolve an existing approval gate.
/// </summary>
/// <param name="ApprovalId">The approval request identifier.</param>
/// <param name="ResolvedBy">The actor resolving the approval request.</param>
public sealed record ResolveApprovalRequest(ApprovalId ApprovalId, string ResolvedBy);
