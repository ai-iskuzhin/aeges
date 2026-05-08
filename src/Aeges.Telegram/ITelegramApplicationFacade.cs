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
    /// Gets a task by identifier.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task lookup result.</returns>
    Task<ApplicationResult<RuntimeTask>> GetTaskAsync(TaskId taskId, CancellationToken cancellationToken);
}
