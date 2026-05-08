using Aeges.Core;
using Aeges.Storage;

namespace Aeges.Application.RunnerExecutions;

/// <summary>
/// Coordinates durable runner process execution tracking use cases.
/// </summary>
public sealed class RunnerExecutionService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunnerExecutionService"/> class.
    /// </summary>
    /// <param name="unitOfWork">The persistence unit of work.</param>
    /// <param name="clock">The deterministic application clock.</param>
    public RunnerExecutionService(IUnitOfWork unitOfWork, IClock clock)
    {
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    /// <summary>
    /// Starts tracking a runner process execution.
    /// </summary>
    /// <param name="request">The runner execution start request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The started execution record, or an expected failure.</returns>
    public async Task<ApplicationResult<RuntimeRunnerExecution>> StartAsync(
        StartRunnerExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateTaskAndIterationAsync(
            request.TaskId,
            request.IterationId,
            request.RunnerId,
            cancellationToken);

        if (!validation.IsSuccess)
        {
            return ApplicationResult<RuntimeRunnerExecution>.Failure(
                validation.Error!.Code,
                validation.Error.Message);
        }

        RuntimeRunnerExecution execution;

        try
        {
            execution = RuntimeRunnerExecution.Start(
                request.RunnerExecutionId ?? RunnerExecutionId.New(),
                request.TaskId,
                request.IterationId,
                request.RunnerId,
                request.Command,
                request.WorkingDirectory,
                clock.Now);
        }
        catch (ArgumentException exception)
        {
            return ApplicationResult<RuntimeRunnerExecution>.Failure("invalid_runner_execution", exception.Message);
        }

        await unitOfWork.RunnerExecutions.AddAsync(execution, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RuntimeRunnerExecution>.Success(execution);
    }

    /// <summary>
    /// Gets runner execution metadata by identifier.
    /// </summary>
    /// <param name="executionId">The runner execution identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The execution record, or an expected failure when it does not exist.</returns>
    public async Task<ApplicationResult<RuntimeRunnerExecution>> GetAsync(
        RunnerExecutionId executionId,
        CancellationToken cancellationToken)
    {
        var execution = await unitOfWork.RunnerExecutions.GetByIdAsync(executionId, cancellationToken);

        return execution is null
            ? ApplicationResult<RuntimeRunnerExecution>.Failure(
                "runner_execution_not_found",
                $"Runner execution '{executionId}' was not found.")
            : ApplicationResult<RuntimeRunnerExecution>.Success(execution);
    }

    /// <summary>
    /// Lists runner executions belonging to a task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task runner executions.</returns>
    public async Task<IReadOnlyList<RuntimeRunnerExecution>> ListByTaskAsync(
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await unitOfWork.RunnerExecutions.ListByTaskAsync(taskId, cancellationToken);

    /// <summary>
    /// Lists runner executions belonging to an iteration.
    /// </summary>
    /// <param name="iterationId">The iteration identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The iteration runner executions.</returns>
    public async Task<IReadOnlyList<RuntimeRunnerExecution>> ListByIterationAsync(
        IterationId iterationId,
        CancellationToken cancellationToken) =>
        await unitOfWork.RunnerExecutions.ListByIterationAsync(iterationId, cancellationToken);

    /// <summary>
    /// Records that a runner process exited with an exit code.
    /// </summary>
    /// <param name="executionId">The runner execution identifier.</param>
    /// <param name="exitCode">The process exit code.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated execution record, or an expected failure.</returns>
    public async Task<ApplicationResult<RuntimeRunnerExecution>> RecordExitAsync(
        RunnerExecutionId executionId,
        int exitCode,
        CancellationToken cancellationToken) =>
        await UpdateAsync(
            executionId,
            (execution, now) => execution.RecordExit(exitCode, now),
            cancellationToken);

    /// <summary>
    /// Records that the runtime stopped a runner process because it exceeded its timeout.
    /// </summary>
    /// <param name="executionId">The runner execution identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated execution record, or an expected failure.</returns>
    public async Task<ApplicationResult<RuntimeRunnerExecution>> RecordTimeoutAsync(
        RunnerExecutionId executionId,
        CancellationToken cancellationToken) =>
        await UpdateAsync(executionId, static (execution, now) => execution.RecordTimeout(now), cancellationToken);

    /// <summary>
    /// Records that the runtime stopped a runner process because cancellation was requested.
    /// </summary>
    /// <param name="executionId">The runner execution identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated execution record, or an expected failure.</returns>
    public async Task<ApplicationResult<RuntimeRunnerExecution>> RecordCancellationAsync(
        RunnerExecutionId executionId,
        CancellationToken cancellationToken) =>
        await UpdateAsync(executionId, static (execution, now) => execution.RecordCancellation(now), cancellationToken);

    private async Task<ApplicationResult<TaskIteration>> ValidateTaskAndIterationAsync(
        TaskId taskId,
        IterationId iterationId,
        RunnerId runnerId,
        CancellationToken cancellationToken)
    {
        var task = await unitOfWork.Tasks.GetByIdAsync(taskId, cancellationToken);

        if (task is null)
        {
            return ApplicationResult<TaskIteration>.Failure("task_not_found", $"Task '{taskId}' was not found.");
        }

        var iteration = await unitOfWork.Iterations.GetByIdAsync(iterationId, cancellationToken);

        if (iteration is null || iteration.TaskId != taskId)
        {
            return ApplicationResult<TaskIteration>.Failure(
                "iteration_not_found",
                $"Iteration '{iterationId}' was not found for task '{taskId}'.");
        }

        if (iteration.RunnerId != runnerId)
        {
            return ApplicationResult<TaskIteration>.Failure(
                "runner_mismatch",
                $"Iteration '{iterationId}' is assigned to runner '{iteration.RunnerId}', not '{runnerId}'.");
        }

        return ApplicationResult<TaskIteration>.Success(iteration);
    }

    private async Task<ApplicationResult<RuntimeRunnerExecution>> UpdateAsync(
        RunnerExecutionId executionId,
        Action<RuntimeRunnerExecution, DateTimeOffset> update,
        CancellationToken cancellationToken)
    {
        var execution = await unitOfWork.RunnerExecutions.GetByIdAsync(executionId, cancellationToken);

        if (execution is null)
        {
            return ApplicationResult<RuntimeRunnerExecution>.Failure(
                "runner_execution_not_found",
                $"Runner execution '{executionId}' was not found.");
        }

        try
        {
            update(execution, clock.Now);
        }
        catch (AegesDomainException exception)
        {
            return ApplicationResult<RuntimeRunnerExecution>.Failure(
                "runner_execution_domain_rule_violation",
                exception.Message);
        }

        await unitOfWork.RunnerExecutions.UpdateAsync(execution, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RuntimeRunnerExecution>.Success(execution);
    }
}
