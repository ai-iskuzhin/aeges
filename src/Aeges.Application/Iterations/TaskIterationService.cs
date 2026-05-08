using Aeges.Core;
using Aeges.Storage;

namespace Aeges.Application.Iterations;

/// <summary>
/// Coordinates bounded task iteration creation and lookup use cases.
/// </summary>
public sealed class TaskIterationService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskIterationService"/> class.
    /// </summary>
    /// <param name="unitOfWork">The persistence unit of work.</param>
    /// <param name="clock">The deterministic application clock.</param>
    public TaskIterationService(IUnitOfWork unitOfWork, IClock clock)
    {
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    /// <summary>
    /// Creates the next bounded iteration for a task and increments the task iteration counter.
    /// </summary>
    /// <param name="request">The task iteration creation request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The created iteration, or an expected failure.</returns>
    public async Task<ApplicationResult<TaskIteration>> CreateNextAsync(
        CreateTaskIterationRequest request,
        CancellationToken cancellationToken)
    {
        var task = await unitOfWork.Tasks.GetByIdAsync(request.TaskId, cancellationToken);

        if (task is null)
        {
            return ApplicationResult<TaskIteration>.Failure("task_not_found", $"Task '{request.TaskId}' was not found.");
        }

        if (IsTerminal(task.Status))
        {
            return ApplicationResult<TaskIteration>.Failure(
                "task_terminal",
                $"Task '{request.TaskId}' is terminal and cannot create another iteration.");
        }

        var now = clock.Now;
        TaskIteration iteration;

        try
        {
            task.AdvanceIteration(now);
            iteration = TaskIteration.Create(
                request.IterationId ?? IterationId.New(),
                request.TaskId,
                task.CurrentIteration,
                request.RunnerId,
                now);
        }
        catch (AegesDomainException exception)
        {
            return ApplicationResult<TaskIteration>.Failure("iteration_limit_reached", exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ApplicationResult<TaskIteration>.Failure("invalid_iteration", exception.Message);
        }

        await unitOfWork.ExecuteInTransactionAsync(
            async transactionCancellationToken =>
            {
                await unitOfWork.Tasks.UpdateAsync(task, transactionCancellationToken);
                await unitOfWork.Iterations.AddAsync(iteration, transactionCancellationToken);
            },
            cancellationToken);

        return ApplicationResult<TaskIteration>.Success(iteration);
    }

    /// <summary>
    /// Gets a task iteration by identifier.
    /// </summary>
    /// <param name="iterationId">The iteration identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching iteration, or an expected failure when it does not exist.</returns>
    public async Task<ApplicationResult<TaskIteration>> GetAsync(
        IterationId iterationId,
        CancellationToken cancellationToken)
    {
        var iteration = await unitOfWork.Iterations.GetByIdAsync(iterationId, cancellationToken);

        return iteration is null
            ? ApplicationResult<TaskIteration>.Failure(
                "iteration_not_found",
                $"Iteration '{iterationId}' was not found.")
            : ApplicationResult<TaskIteration>.Success(iteration);
    }

    /// <summary>
    /// Lists iterations belonging to a task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task iterations.</returns>
    public async Task<IReadOnlyList<TaskIteration>> ListByTaskAsync(
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await unitOfWork.Iterations.ListByTaskAsync(taskId, cancellationToken);

    /// <summary>
    /// Moves an iteration into runner execution.
    /// </summary>
    /// <param name="iterationId">The iteration identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated iteration, or an expected failure.</returns>
    public async Task<ApplicationResult<TaskIteration>> StartRunningAsync(
        IterationId iterationId,
        CancellationToken cancellationToken) =>
        await UpdateAsync(iterationId, static (iteration, now) => iteration.StartRunning(now), cancellationToken);

    /// <summary>
    /// Moves an iteration into review.
    /// </summary>
    /// <param name="iterationId">The iteration identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated iteration, or an expected failure.</returns>
    public async Task<ApplicationResult<TaskIteration>> StartReviewAsync(
        IterationId iterationId,
        CancellationToken cancellationToken) =>
        await UpdateAsync(iterationId, static (iteration, now) => iteration.StartReview(now), cancellationToken);

    /// <summary>
    /// Pauses an iteration until a required approval is resolved.
    /// </summary>
    /// <param name="iterationId">The iteration identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated iteration, or an expected failure.</returns>
    public async Task<ApplicationResult<TaskIteration>> WaitForApprovalAsync(
        IterationId iterationId,
        CancellationToken cancellationToken) =>
        await UpdateAsync(iterationId, static (iteration, now) => iteration.WaitForApproval(now), cancellationToken);

    /// <summary>
    /// Marks an iteration as completed.
    /// </summary>
    /// <param name="iterationId">The iteration identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated iteration, or an expected failure.</returns>
    public async Task<ApplicationResult<TaskIteration>> CompleteAsync(
        IterationId iterationId,
        CancellationToken cancellationToken) =>
        await UpdateAsync(iterationId, static (iteration, now) => iteration.Complete(now), cancellationToken);

    /// <summary>
    /// Marks an iteration as failed.
    /// </summary>
    /// <param name="iterationId">The iteration identifier.</param>
    /// <param name="failureReason">The reason the iteration failed.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated iteration, or an expected failure.</returns>
    public async Task<ApplicationResult<TaskIteration>> FailAsync(
        IterationId iterationId,
        string failureReason,
        CancellationToken cancellationToken) =>
        await UpdateAsync(iterationId, (iteration, now) => iteration.Fail(failureReason, now), cancellationToken);

    /// <summary>
    /// Cancels an iteration.
    /// </summary>
    /// <param name="iterationId">The iteration identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated iteration, or an expected failure.</returns>
    public async Task<ApplicationResult<TaskIteration>> CancelAsync(
        IterationId iterationId,
        CancellationToken cancellationToken) =>
        await UpdateAsync(iterationId, static (iteration, now) => iteration.Cancel(now), cancellationToken);

    private async Task<ApplicationResult<TaskIteration>> UpdateAsync(
        IterationId iterationId,
        Action<TaskIteration, DateTimeOffset> update,
        CancellationToken cancellationToken)
    {
        var iteration = await unitOfWork.Iterations.GetByIdAsync(iterationId, cancellationToken);

        if (iteration is null)
        {
            return ApplicationResult<TaskIteration>.Failure(
                "iteration_not_found",
                $"Iteration '{iterationId}' was not found.");
        }

        try
        {
            update(iteration, clock.Now);
        }
        catch (InvalidTaskIterationStatusTransitionException exception)
        {
            return ApplicationResult<TaskIteration>.Failure("invalid_iteration_status_transition", exception.Message);
        }
        catch (AegesDomainException exception)
        {
            return ApplicationResult<TaskIteration>.Failure("iteration_domain_rule_violation", exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ApplicationResult<TaskIteration>.Failure("invalid_iteration", exception.Message);
        }

        await unitOfWork.Iterations.UpdateAsync(iteration, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApplicationResult<TaskIteration>.Success(iteration);
    }

    private static bool IsTerminal(RuntimeTaskStatus status) =>
        status is RuntimeTaskStatus.Completed or RuntimeTaskStatus.Failed or RuntimeTaskStatus.Cancelled;
}
