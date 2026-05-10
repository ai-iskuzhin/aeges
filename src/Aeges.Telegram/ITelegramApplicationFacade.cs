using Aeges.Application;
using Aeges.Core;

namespace Aeges.Telegram;

/// <summary>
/// Defines application operations exposed to the Telegram transport.
/// </summary>
public interface ITelegramApplicationFacade
{
    /// <summary>
    /// Lists registered projects.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The registered projects.</returns>
    Task<IReadOnlyList<RuntimeProject>> ListProjectsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lists registered machines.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The registered machines.</returns>
    Task<IReadOnlyList<RuntimeMachine>> ListMachinesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lists queued tasks.
    /// </summary>
    /// <param name="limit">The maximum number of tasks to return.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The queued tasks.</returns>
    Task<IReadOnlyList<RuntimeTask>> ListQueuedTasksAsync(int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Lists tasks by lifecycle status.
    /// </summary>
    /// <param name="status">The lifecycle status to list.</param>
    /// <param name="limit">The maximum number of tasks to return.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching tasks.</returns>
    Task<IReadOnlyList<RuntimeTask>> ListTasksByStatusAsync(
        RuntimeTaskStatus status,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists pending approval requests.
    /// </summary>
    /// <param name="limit">The maximum number of approvals to return.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The pending approvals.</returns>
    Task<IReadOnlyList<ApprovalRequest>> ListPendingApprovalsAsync(int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a queued runtime task.
    /// </summary>
    /// <param name="projectId">The project that owns the task.</param>
    /// <param name="machineId">The machine assigned to process the task.</param>
    /// <param name="title">The task title.</param>
    /// <param name="goal">The task goal.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task creation result.</returns>
    Task<ApplicationResult<RuntimeTask>> CreateTaskAsync(
        ProjectId projectId,
        MachineId machineId,
        string title,
        string goal,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets a task by identifier.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task lookup result.</returns>
    Task<ApplicationResult<RuntimeTask>> GetTaskAsync(TaskId taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets task review details, including iterations, artifacts, runner executions, and an output preview.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task review snapshot, or an expected failure.</returns>
    Task<ApplicationResult<TelegramTaskReviewSnapshot>> GetTaskReviewAsync(
        TaskId taskId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cancels a task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task cancellation result.</returns>
    Task<ApplicationResult<RuntimeTask>> CancelTaskAsync(TaskId taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Completes a task after review.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task completion result.</returns>
    Task<ApplicationResult<RuntimeTask>> CompleteTaskAsync(TaskId taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets an approval request by identifier.
    /// </summary>
    /// <param name="approvalId">The approval request identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The approval lookup result.</returns>
    Task<ApplicationResult<ApprovalRequest>> GetApprovalAsync(ApprovalId approvalId, CancellationToken cancellationToken);

    /// <summary>
    /// Approves a pending approval request.
    /// </summary>
    /// <param name="approvalId">The approval request identifier.</param>
    /// <param name="resolvedBy">The actor resolving the approval.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The approval resolution result.</returns>
    Task<ApplicationResult<ApprovalRequest>> ApproveApprovalAsync(
        ApprovalId approvalId,
        string resolvedBy,
        CancellationToken cancellationToken);

    /// <summary>
    /// Rejects a pending approval request.
    /// </summary>
    /// <param name="approvalId">The approval request identifier.</param>
    /// <param name="resolvedBy">The actor resolving the approval.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The approval resolution result.</returns>
    Task<ApplicationResult<ApprovalRequest>> RejectApprovalAsync(
        ApprovalId approvalId,
        string resolvedBy,
        CancellationToken cancellationToken);
}
