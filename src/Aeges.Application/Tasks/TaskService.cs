using Aeges.Core;
using Aeges.Storage;

namespace Aeges.Application.Tasks;

/// <summary>
/// Coordinates durable task creation and query use cases.
/// </summary>
public sealed class TaskService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskService"/> class.
    /// </summary>
    /// <param name="unitOfWork">The persistence unit of work.</param>
    /// <param name="clock">The deterministic application clock.</param>
    public TaskService(IUnitOfWork unitOfWork, IClock clock)
    {
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    /// <summary>
    /// Creates a queued runtime task.
    /// </summary>
    /// <param name="request">The task creation request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The created task, or an expected failure when dependencies are missing.</returns>
    public async Task<ApplicationResult<RuntimeTask>> CreateAsync(
        CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var project = await unitOfWork.Projects.GetByIdAsync(request.ProjectId, cancellationToken);

        if (project is null)
        {
            return ApplicationResult<RuntimeTask>.Failure(
                "project_not_found",
                $"Project '{request.ProjectId}' was not found.");
        }

        if (project.IsArchived)
        {
            return ApplicationResult<RuntimeTask>.Failure(
                "project_archived",
                $"Project '{request.ProjectId}' is archived and cannot accept new tasks.");
        }

        var machine = await unitOfWork.Machines.GetByIdAsync(request.MachineId, cancellationToken);

        if (machine is null)
        {
            return ApplicationResult<RuntimeTask>.Failure(
                "machine_not_found",
                $"Machine '{request.MachineId}' was not found.");
        }

        var task = RuntimeTask.Create(
            request.TaskId ?? TaskId.New(),
            request.ProjectId,
            request.MachineId,
            request.Title,
            request.Goal,
            clock.Now,
            request.Priority,
            request.MaxIterations);

        await unitOfWork.Tasks.AddAsync(task, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RuntimeTask>.Success(task);
    }

    /// <summary>
    /// Gets a task by identifier.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching task, or an expected failure when no task exists.</returns>
    public async Task<ApplicationResult<RuntimeTask>> GetAsync(
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var task = await unitOfWork.Tasks.GetByIdAsync(taskId, cancellationToken);

        return task is null
            ? ApplicationResult<RuntimeTask>.Failure("task_not_found", $"Task '{taskId}' was not found.")
            : ApplicationResult<RuntimeTask>.Success(task);
    }

    /// <summary>
    /// Lists tasks belonging to a project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The project tasks.</returns>
    public async Task<IReadOnlyList<RuntimeTask>> ListByProjectAsync(
        ProjectId projectId,
        CancellationToken cancellationToken) =>
        await unitOfWork.Tasks.ListByProjectAsync(projectId, cancellationToken);

    /// <summary>
    /// Lists tasks matching a lifecycle status.
    /// </summary>
    /// <param name="status">The lifecycle status to match.</param>
    /// <param name="limit">The maximum number of tasks to return.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching tasks.</returns>
    public async Task<IReadOnlyList<RuntimeTask>> ListByStatusAsync(
        RuntimeTaskStatus status,
        int limit,
        CancellationToken cancellationToken) =>
        await unitOfWork.Tasks.ListByStatusAsync(status, limit, cancellationToken);

    /// <summary>
    /// Moves a task into planning.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated task, or an expected failure.</returns>
    public async Task<ApplicationResult<RuntimeTask>> StartPlanningAsync(
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await UpdateTaskAsync(taskId, static (task, now) => task.StartPlanning(now), cancellationToken);

    /// <summary>
    /// Moves a task into runner execution.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated task, or an expected failure.</returns>
    public async Task<ApplicationResult<RuntimeTask>> StartRunningAsync(
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await UpdateTaskAsync(taskId, static (task, now) => task.StartRunning(now), cancellationToken);

    /// <summary>
    /// Moves a task into review.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated task, or an expected failure.</returns>
    public async Task<ApplicationResult<RuntimeTask>> StartReviewAsync(
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await UpdateTaskAsync(taskId, static (task, now) => task.StartReview(now), cancellationToken);

    /// <summary>
    /// Requeues a reviewed task for another bounded iteration.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated task, or an expected failure.</returns>
    public async Task<ApplicationResult<RuntimeTask>> RequeueForRevisionAsync(
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await UpdateTaskAsync(taskId, static (task, now) => task.RequeueForRevision(now), cancellationToken);

    /// <summary>
    /// Pauses a task until approval is resolved.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated task, or an expected failure.</returns>
    public async Task<ApplicationResult<RuntimeTask>> WaitForApprovalAsync(
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await UpdateTaskAsync(taskId, static (task, now) => task.WaitForApproval(now), cancellationToken);

    /// <summary>
    /// Marks a task as completed.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated task, or an expected failure.</returns>
    public async Task<ApplicationResult<RuntimeTask>> CompleteAsync(
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await UpdateTaskAsync(taskId, static (task, now) => task.Complete(now), cancellationToken);

    /// <summary>
    /// Marks a task as failed.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="failureReason">The reason the task failed.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated task, or an expected failure.</returns>
    public async Task<ApplicationResult<RuntimeTask>> FailAsync(
        TaskId taskId,
        string failureReason,
        CancellationToken cancellationToken) =>
        await UpdateTaskAsync(taskId, (task, now) => task.Fail(failureReason, now), cancellationToken);

    /// <summary>
    /// Cancels a task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated task, or an expected failure.</returns>
    public async Task<ApplicationResult<RuntimeTask>> CancelAsync(
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await UpdateTaskAsync(taskId, static (task, now) => task.Cancel(now), cancellationToken);

    /// <summary>
    /// Advances a task to the next bounded iteration.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated task, or an expected failure.</returns>
    public async Task<ApplicationResult<RuntimeTask>> AdvanceIterationAsync(
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await UpdateTaskAsync(taskId, static (task, now) => task.AdvanceIteration(now), cancellationToken);

    private async Task<ApplicationResult<RuntimeTask>> UpdateTaskAsync(
        TaskId taskId,
        Action<RuntimeTask, DateTimeOffset> update,
        CancellationToken cancellationToken)
    {
        var task = await unitOfWork.Tasks.GetByIdAsync(taskId, cancellationToken);

        if (task is null)
        {
            return ApplicationResult<RuntimeTask>.Failure("task_not_found", $"Task '{taskId}' was not found.");
        }

        try
        {
            update(task, clock.Now);
        }
        catch (InvalidTaskStatusTransitionException exception)
        {
            return ApplicationResult<RuntimeTask>.Failure("invalid_task_status_transition", exception.Message);
        }
        catch (AegesDomainException exception)
        {
            return ApplicationResult<RuntimeTask>.Failure("task_domain_rule_violation", exception.Message);
        }

        await unitOfWork.Tasks.UpdateAsync(task, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RuntimeTask>.Success(task);
    }
}
