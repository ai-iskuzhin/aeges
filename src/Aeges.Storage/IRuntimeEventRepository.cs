using Aeges.Core;

namespace Aeges.Storage;

/// <summary>
/// Provides persistence operations for auditable runtime events.
/// </summary>
public interface IRuntimeEventRepository
{
    /// <summary>
    /// Adds a runtime event to storage.
    /// </summary>
    /// <param name="runtimeEvent">The runtime event to add.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken);

    /// <summary>
    /// Lists runtime events belonging to a task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="limit">The maximum number of most recent events to return.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task runtime events in chronological order.</returns>
    Task<IReadOnlyList<RuntimeEvent>> ListByTaskAsync(
        TaskId taskId,
        int limit,
        CancellationToken cancellationToken);
}
