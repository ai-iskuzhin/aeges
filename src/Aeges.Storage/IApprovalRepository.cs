using Aeges.Core;

namespace Aeges.Storage;

/// <summary>
/// Provides persistence operations for approval requests.
/// </summary>
public interface IApprovalRepository
{
    /// <summary>
    /// Adds an approval request to storage.
    /// </summary>
    /// <param name="approval">The approval request to add.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(ApprovalRequest approval, CancellationToken cancellationToken);

    /// <summary>
    /// Gets an approval request by identifier.
    /// </summary>
    /// <param name="id">The approval request identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching approval request, or <see langword="null"/> when none exists.</returns>
    Task<ApprovalRequest?> GetByIdAsync(ApprovalId id, CancellationToken cancellationToken);

    /// <summary>
    /// Lists approval requests belonging to a task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task approvals.</returns>
    Task<IReadOnlyList<ApprovalRequest>> ListByTaskAsync(TaskId taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists currently pending approval requests.
    /// </summary>
    /// <param name="limit">The maximum number of approvals to return.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The pending approvals.</returns>
    Task<IReadOnlyList<ApprovalRequest>> ListPendingAsync(int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an approval request in storage.
    /// </summary>
    /// <param name="approval">The approval request to update.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(ApprovalRequest approval, CancellationToken cancellationToken);
}
