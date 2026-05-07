namespace Aeges.Core;

/// <summary>
/// Represents a durable approval gate in a governed task workflow.
/// </summary>
public sealed class ApprovalRequest
{
    private ApprovalRequest(
        ApprovalId id,
        TaskId taskId,
        IterationId? iterationId,
        string reason,
        string requestedAction,
        DateTimeOffset createdAt)
    {
        Id = id;
        TaskId = taskId;
        IterationId = iterationId;
        Reason = RequireText(reason, nameof(reason));
        RequestedAction = RequireText(requestedAction, nameof(requestedAction));
        CreatedAt = createdAt;
        Status = ApprovalStatus.Pending;
    }

    /// <summary>
    /// Gets the approval request identifier.
    /// </summary>
    public ApprovalId Id { get; }

    /// <summary>
    /// Gets the task that owns the approval request.
    /// </summary>
    public TaskId TaskId { get; }

    /// <summary>
    /// Gets the iteration associated with the approval request, when applicable.
    /// </summary>
    public IterationId? IterationId { get; }

    /// <summary>
    /// Gets the current approval status.
    /// </summary>
    public ApprovalStatus Status { get; private set; }

    /// <summary>
    /// Gets the reason approval is required.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Gets the action being requested.
    /// </summary>
    public string RequestedAction { get; }

    /// <summary>
    /// Gets the timestamp when approval was requested.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the timestamp when approval was resolved.
    /// </summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>
    /// Gets the actor that resolved approval, when known.
    /// </summary>
    public string? ResolvedBy { get; private set; }

    /// <summary>
    /// Creates a new pending approval request.
    /// </summary>
    /// <param name="id">The approval request identifier.</param>
    /// <param name="taskId">The task that owns the approval request.</param>
    /// <param name="iterationId">The iteration associated with the approval request, when applicable.</param>
    /// <param name="reason">The reason approval is required.</param>
    /// <param name="requestedAction">The action being requested.</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <returns>A pending approval request.</returns>
    public static ApprovalRequest Create(
        ApprovalId id,
        TaskId taskId,
        IterationId? iterationId,
        string reason,
        string requestedAction,
        DateTimeOffset createdAt) =>
        new(id, taskId, iterationId, reason, requestedAction, createdAt);

    /// <summary>
    /// Rehydrates an approval request from durable storage.
    /// </summary>
    /// <param name="id">The approval request identifier.</param>
    /// <param name="taskId">The task that owns the approval request.</param>
    /// <param name="iterationId">The iteration associated with the approval request, when applicable.</param>
    /// <param name="status">The current approval status.</param>
    /// <param name="reason">The reason approval is required.</param>
    /// <param name="requestedAction">The action being requested.</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="resolvedAt">The timestamp when approval was resolved.</param>
    /// <param name="resolvedBy">The actor that resolved approval, when known.</param>
    /// <returns>A rehydrated approval request.</returns>
    public static ApprovalRequest Rehydrate(
        ApprovalId id,
        TaskId taskId,
        IterationId? iterationId,
        ApprovalStatus status,
        string reason,
        string requestedAction,
        DateTimeOffset createdAt,
        DateTimeOffset? resolvedAt,
        string? resolvedBy)
    {
        var approval = new ApprovalRequest(id, taskId, iterationId, reason, requestedAction, createdAt)
        {
            Status = status,
            ResolvedAt = resolvedAt,
            ResolvedBy = resolvedBy,
        };

        return approval;
    }

    /// <summary>
    /// Approves the requested action.
    /// </summary>
    /// <param name="resolvedBy">The actor that approved the request.</param>
    /// <param name="now">The resolution timestamp.</param>
    public void Approve(string resolvedBy, DateTimeOffset now) =>
        Resolve(ApprovalStatus.Approved, resolvedBy, now);

    /// <summary>
    /// Rejects the requested action.
    /// </summary>
    /// <param name="resolvedBy">The actor that rejected the request.</param>
    /// <param name="now">The resolution timestamp.</param>
    public void Reject(string resolvedBy, DateTimeOffset now) =>
        Resolve(ApprovalStatus.Rejected, resolvedBy, now);

    /// <summary>
    /// Cancels the approval request before a decision is made.
    /// </summary>
    /// <param name="resolvedBy">The actor that cancelled the request.</param>
    /// <param name="now">The cancellation timestamp.</param>
    public void Cancel(string resolvedBy, DateTimeOffset now) =>
        Resolve(ApprovalStatus.Cancelled, resolvedBy, now);

    private void Resolve(ApprovalStatus status, string resolvedBy, DateTimeOffset now)
    {
        if (Status.IsTerminal())
        {
            throw new AegesDomainException($"Approval request '{Id}' is already resolved.");
        }

        Status = status;
        ResolvedBy = RequireText(resolvedBy, nameof(resolvedBy));
        ResolvedAt = now;
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value;
    }
}
