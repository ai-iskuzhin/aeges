using Aeges.Core;

namespace Aeges.Storage;

/// <summary>
/// Provides persistence operations for runtime tasks.
/// </summary>
public interface ITaskRepository
{
    /// <summary>
    /// Adds a task to storage.
    /// </summary>
    /// <param name="task">The task to add.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(RuntimeTask task, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a task by identifier.
    /// </summary>
    /// <param name="id">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching task, or <see langword="null"/> when none exists.</returns>
    Task<RuntimeTask?> GetByIdAsync(TaskId id, CancellationToken cancellationToken);

    /// <summary>
    /// Lists tasks belonging to a project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The project tasks.</returns>
    Task<IReadOnlyList<RuntimeTask>> ListByProjectAsync(ProjectId projectId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists tasks matching a lifecycle status.
    /// </summary>
    /// <param name="status">The lifecycle status to match.</param>
    /// <param name="limit">The maximum number of tasks to return.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching tasks.</returns>
    Task<IReadOnlyList<RuntimeTask>> ListByStatusAsync(
        RuntimeTaskStatus status,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates a task in storage.
    /// </summary>
    /// <param name="task">The task to update.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(RuntimeTask task, CancellationToken cancellationToken);
}
