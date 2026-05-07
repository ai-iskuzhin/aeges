using Aeges.Core;

namespace Aeges.Storage;

/// <summary>
/// Provides persistence operations for path-based runtime locks.
/// </summary>
public interface ILockRepository
{
    /// <summary>
    /// Adds a lock to storage.
    /// </summary>
    /// <param name="runtimeLock">The lock to add.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(RuntimeLock runtimeLock, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a lock by identifier.
    /// </summary>
    /// <param name="id">The lock identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching lock, or <see langword="null"/> when none exists.</returns>
    Task<RuntimeLock?> GetByIdAsync(LockId id, CancellationToken cancellationToken);

    /// <summary>
    /// Lists active locks for a project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The active project locks.</returns>
    Task<IReadOnlyList<RuntimeLock>> ListActiveByProjectAsync(ProjectId projectId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists active locks held by a task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The active task locks.</returns>
    Task<IReadOnlyList<RuntimeLock>> ListActiveByTaskAsync(TaskId taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Updates a lock in storage.
    /// </summary>
    /// <param name="runtimeLock">The lock to update.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(RuntimeLock runtimeLock, CancellationToken cancellationToken);
}
