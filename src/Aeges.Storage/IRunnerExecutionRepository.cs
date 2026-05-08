using Aeges.Core;

namespace Aeges.Storage;

/// <summary>
/// Provides persistence operations for runner process execution metadata.
/// </summary>
public interface IRunnerExecutionRepository
{
    /// <summary>
    /// Adds runner execution metadata to storage.
    /// </summary>
    /// <param name="execution">The runner execution metadata to add.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(RuntimeRunnerExecution execution, CancellationToken cancellationToken);

    /// <summary>
    /// Gets runner execution metadata by identifier.
    /// </summary>
    /// <param name="id">The runner execution identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching runner execution metadata, or <see langword="null"/> when none exists.</returns>
    Task<RuntimeRunnerExecution?> GetByIdAsync(RunnerExecutionId id, CancellationToken cancellationToken);

    /// <summary>
    /// Lists runner executions belonging to a task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task runner executions.</returns>
    Task<IReadOnlyList<RuntimeRunnerExecution>> ListByTaskAsync(TaskId taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists runner executions belonging to an iteration.
    /// </summary>
    /// <param name="iterationId">The iteration identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The iteration runner executions.</returns>
    Task<IReadOnlyList<RuntimeRunnerExecution>> ListByIterationAsync(
        IterationId iterationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates runner execution metadata in storage.
    /// </summary>
    /// <param name="execution">The runner execution metadata to update.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(RuntimeRunnerExecution execution, CancellationToken cancellationToken);
}
