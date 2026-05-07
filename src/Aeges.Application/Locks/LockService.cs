using Aeges.Core;
using Aeges.Storage;

namespace Aeges.Application.Locks;

/// <summary>
/// Coordinates path-based lock acquisition, conflict detection, and release use cases.
/// </summary>
public sealed class LockService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="LockService"/> class.
    /// </summary>
    /// <param name="unitOfWork">The persistence unit of work.</param>
    /// <param name="clock">The deterministic application clock.</param>
    public LockService(IUnitOfWork unitOfWork, IClock clock)
    {
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    /// <summary>
    /// Acquires a lock when no active conflicting lock exists.
    /// </summary>
    /// <param name="request">The lock acquisition request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The acquired lock, or an expected failure.</returns>
    public async Task<ApplicationResult<RuntimeLock>> AcquireAsync(
        AcquireLockRequest request,
        CancellationToken cancellationToken)
    {
        var task = await unitOfWork.Tasks.GetByIdAsync(request.TaskId, cancellationToken);

        if (task is null)
        {
            return ApplicationResult<RuntimeLock>.Failure("task_not_found", $"Task '{request.TaskId}' was not found.");
        }

        if (task.ProjectId != request.ProjectId)
        {
            return ApplicationResult<RuntimeLock>.Failure(
                "project_mismatch",
                $"Task '{request.TaskId}' does not belong to project '{request.ProjectId}'.");
        }

        RuntimeLock runtimeLock;

        try
        {
            runtimeLock = RuntimeLock.Acquire(
                request.LockId ?? LockId.New(),
                request.TaskId,
                request.ProjectId,
                request.PathPattern,
                clock.Now);
        }
        catch (ArgumentException exception)
        {
            return ApplicationResult<RuntimeLock>.Failure("invalid_lock_path", exception.Message);
        }

        var activeLocks = await unitOfWork.Locks.ListActiveByProjectAsync(request.ProjectId, cancellationToken);
        var conflictingLock = activeLocks.FirstOrDefault(runtimeLock.ConflictsWith);

        if (conflictingLock is not null)
        {
            return ApplicationResult<RuntimeLock>.Failure(
                "lock_conflict",
                $"Lock '{request.PathPattern}' conflicts with active lock '{conflictingLock.Id}' on '{conflictingLock.PathPattern}'.");
        }

        await unitOfWork.Locks.AddAsync(runtimeLock, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RuntimeLock>.Success(runtimeLock);
    }

    /// <summary>
    /// Releases an active lock.
    /// </summary>
    /// <param name="lockId">The lock identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The released lock, or an expected failure.</returns>
    public async Task<ApplicationResult<RuntimeLock>> ReleaseAsync(
        LockId lockId,
        CancellationToken cancellationToken)
    {
        var runtimeLock = await unitOfWork.Locks.GetByIdAsync(lockId, cancellationToken);

        if (runtimeLock is null)
        {
            return ApplicationResult<RuntimeLock>.Failure("lock_not_found", $"Lock '{lockId}' was not found.");
        }

        try
        {
            runtimeLock.Release(clock.Now);
        }
        catch (AegesDomainException exception)
        {
            return ApplicationResult<RuntimeLock>.Failure("lock_already_released", exception.Message);
        }

        await unitOfWork.Locks.UpdateAsync(runtimeLock, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RuntimeLock>.Success(runtimeLock);
    }

    /// <summary>
    /// Lists active locks for a project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The active project locks.</returns>
    public async Task<IReadOnlyList<RuntimeLock>> ListActiveByProjectAsync(
        ProjectId projectId,
        CancellationToken cancellationToken) =>
        await unitOfWork.Locks.ListActiveByProjectAsync(projectId, cancellationToken);

    /// <summary>
    /// Lists active locks held by a task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The active task locks.</returns>
    public async Task<IReadOnlyList<RuntimeLock>> ListActiveByTaskAsync(
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await unitOfWork.Locks.ListActiveByTaskAsync(taskId, cancellationToken);
}
