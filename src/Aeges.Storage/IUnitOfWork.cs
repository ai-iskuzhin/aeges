namespace Aeges.Storage;

/// <summary>
/// Coordinates repository operations inside a persistence unit of work.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Gets the task repository.
    /// </summary>
    ITaskRepository Tasks { get; }

    /// <summary>
    /// Gets the task iteration repository.
    /// </summary>
    IIterationRepository Iterations { get; }

    /// <summary>
    /// Gets the artifact repository.
    /// </summary>
    IArtifactRepository Artifacts { get; }

    /// <summary>
    /// Gets the approval repository.
    /// </summary>
    IApprovalRepository Approvals { get; }

    /// <summary>
    /// Gets the project repository.
    /// </summary>
    IProjectRepository Projects { get; }

    /// <summary>
    /// Gets the machine repository.
    /// </summary>
    IMachineRepository Machines { get; }

    /// <summary>
    /// Gets the lock repository.
    /// </summary>
    ILockRepository Locks { get; }

    /// <summary>
    /// Gets the runner execution repository.
    /// </summary>
    IRunnerExecutionRepository RunnerExecutions { get; }

    /// <summary>
    /// Persists pending changes.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The number of persisted changes, when the implementation can report it.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Executes an operation in a transaction.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes an operation in a transaction and returns its result.
    /// </summary>
    /// <typeparam name="TResult">The operation result type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The operation result.</returns>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}
