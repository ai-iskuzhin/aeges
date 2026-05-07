using Aeges.Core;

namespace Aeges.Storage;

/// <summary>
/// Provides persistence operations for task iterations.
/// </summary>
public interface IIterationRepository
{
    /// <summary>
    /// Adds an iteration to storage.
    /// </summary>
    /// <param name="iteration">The iteration to add.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(TaskIteration iteration, CancellationToken cancellationToken);

    /// <summary>
    /// Gets an iteration by identifier.
    /// </summary>
    /// <param name="id">The iteration identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching iteration, or <see langword="null"/> when none exists.</returns>
    Task<TaskIteration?> GetByIdAsync(IterationId id, CancellationToken cancellationToken);

    /// <summary>
    /// Lists iterations belonging to a task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task iterations.</returns>
    Task<IReadOnlyList<TaskIteration>> ListByTaskAsync(TaskId taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an iteration in storage.
    /// </summary>
    /// <param name="iteration">The iteration to update.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(TaskIteration iteration, CancellationToken cancellationToken);
}
