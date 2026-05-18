namespace Aeges.Core;

/// <summary>
/// Represents a durable unit of governed coding work owned by the Aeges runtime.
/// </summary>
public sealed class RuntimeTask
{
    /// <summary>
    /// The default number of runner iterations allowed for a task.
    /// </summary>
    public const int DefaultMaxIterations = 10;

    private RuntimeTask(
        TaskId id,
        ProjectId projectId,
        MachineId machineId,
        string title,
        string goal,
        int priority,
        int maxIterations,
        DateTimeOffset createdAt)
    {
        Id = id;
        ProjectId = projectId;
        MachineId = machineId;
        Title = RequireText(title, nameof(title));
        Goal = RequireText(goal, nameof(goal));
        Priority = priority;
        MaxIterations = RequirePositive(maxIterations, nameof(maxIterations));
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        Status = RuntimeTaskStatus.Queued;
    }

    /// <summary>
    /// Gets the task identifier.
    /// </summary>
    public TaskId Id { get; }

    /// <summary>
    /// Gets the project this task belongs to.
    /// </summary>
    public ProjectId ProjectId { get; }

    /// <summary>
    /// Gets the machine assigned to own or execute this task.
    /// </summary>
    public MachineId MachineId { get; }

    /// <summary>
    /// Gets the human-readable task title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the task goal supplied to the runtime.
    /// </summary>
    public string Goal { get; }

    /// <summary>
    /// Gets the current lifecycle status.
    /// </summary>
    public RuntimeTaskStatus Status { get; private set; }

    /// <summary>
    /// Gets the scheduling priority for this task.
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// Gets the maximum number of iterations allowed for this task.
    /// </summary>
    public int MaxIterations { get; }

    /// <summary>
    /// Gets the current iteration number.
    /// </summary>
    public int CurrentIteration { get; private set; }

    /// <summary>
    /// Gets the timestamp when the task was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the timestamp when the task was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Gets the timestamp when runtime work first started for the task.
    /// </summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>
    /// Gets the timestamp when the task completed successfully.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// Gets the timestamp when the task was cancelled.
    /// </summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>
    /// Gets the reason recorded when the task fails.
    /// </summary>
    public string? FailureReason { get; private set; }

    /// <summary>
    /// Creates a new queued runtime task.
    /// </summary>
    /// <param name="id">The task identifier.</param>
    /// <param name="projectId">The project this task belongs to.</param>
    /// <param name="machineId">The machine assigned to the task.</param>
    /// <param name="title">The human-readable task title.</param>
    /// <param name="goal">The task goal.</param>
    /// <param name="createdAt">The task creation timestamp.</param>
    /// <param name="priority">The task scheduling priority.</param>
    /// <param name="maxIterations">The maximum number of allowed iterations.</param>
    /// <returns>A queued runtime task.</returns>
    public static RuntimeTask Create(
        TaskId id,
        ProjectId projectId,
        MachineId machineId,
        string title,
        string goal,
        DateTimeOffset createdAt,
        int priority = 0,
        int maxIterations = DefaultMaxIterations) =>
        new(id, projectId, machineId, title, goal, priority, maxIterations, createdAt);

    /// <summary>
    /// Rehydrates a runtime task from durable storage.
    /// </summary>
    /// <param name="id">The task identifier.</param>
    /// <param name="projectId">The project this task belongs to.</param>
    /// <param name="machineId">The machine assigned to the task.</param>
    /// <param name="title">The human-readable task title.</param>
    /// <param name="goal">The task goal.</param>
    /// <param name="status">The current lifecycle status.</param>
    /// <param name="priority">The task scheduling priority.</param>
    /// <param name="maxIterations">The maximum number of allowed iterations.</param>
    /// <param name="currentIteration">The current iteration number.</param>
    /// <param name="createdAt">The task creation timestamp.</param>
    /// <param name="updatedAt">The last update timestamp.</param>
    /// <param name="startedAt">The timestamp when runtime work first started for the task.</param>
    /// <param name="completedAt">The timestamp when the task completed successfully.</param>
    /// <param name="cancelledAt">The timestamp when the task was cancelled.</param>
    /// <param name="failureReason">The reason recorded when the task failed.</param>
    /// <returns>A rehydrated runtime task.</returns>
    public static RuntimeTask Rehydrate(
        TaskId id,
        ProjectId projectId,
        MachineId machineId,
        string title,
        string goal,
        RuntimeTaskStatus status,
        int priority,
        int maxIterations,
        int currentIteration,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        DateTimeOffset? cancelledAt,
        string? failureReason)
    {
        if (currentIteration < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentIteration), currentIteration, "Value must not be negative.");
        }

        if (currentIteration > maxIterations)
        {
            throw new ArgumentOutOfRangeException(nameof(currentIteration), currentIteration, "Value must not exceed max iterations.");
        }

        var task = new RuntimeTask(id, projectId, machineId, title, goal, priority, maxIterations, createdAt)
        {
            Status = status,
            CurrentIteration = currentIteration,
            UpdatedAt = updatedAt,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            CancelledAt = cancelledAt,
            FailureReason = failureReason,
        };

        return task;
    }

    /// <summary>
    /// Moves the task from queued execution into planning.
    /// </summary>
    /// <param name="now">The transition timestamp.</param>
    public void StartPlanning(DateTimeOffset now)
    {
        TransitionTo(RuntimeTaskStatus.Planning, now);
        StartedAt ??= now;
    }

    /// <summary>
    /// Moves the task into runner execution.
    /// </summary>
    /// <param name="now">The transition timestamp.</param>
    public void StartRunning(DateTimeOffset now)
    {
        TransitionTo(RuntimeTaskStatus.Running, now);
        StartedAt ??= now;
    }

    /// <summary>
    /// Moves the task into review after runner execution.
    /// </summary>
    /// <param name="now">The transition timestamp.</param>
    public void StartReview(DateTimeOffset now) => TransitionTo(RuntimeTaskStatus.Reviewing, now);

    /// <summary>
    /// Requeues a reviewed task for another bounded iteration.
    /// </summary>
    /// <param name="now">The transition timestamp.</param>
    public void RequeueForRevision(DateTimeOffset now)
    {
        if (CurrentIteration >= MaxIterations)
        {
            throw new AegesDomainException($"Task '{Id}' cannot continue because it reached {MaxIterations} iterations.");
        }

        TransitionTo(RuntimeTaskStatus.Queued, now);
        CancelledAt = null;
    }

    /// <summary>
    /// Pauses the task until a required approval is resolved.
    /// </summary>
    /// <param name="now">The transition timestamp.</param>
    public void WaitForApproval(DateTimeOffset now) => TransitionTo(RuntimeTaskStatus.WaitingApproval, now);

    /// <summary>
    /// Marks the task as successfully completed.
    /// </summary>
    /// <param name="now">The completion timestamp.</param>
    public void Complete(DateTimeOffset now)
    {
        TransitionTo(RuntimeTaskStatus.Completed, now);
        CompletedAt = now;
    }

    /// <summary>
    /// Marks the task as failed with a durable reason.
    /// </summary>
    /// <param name="failureReason">The reason the task failed.</param>
    /// <param name="now">The failure timestamp.</param>
    public void Fail(string failureReason, DateTimeOffset now)
    {
        FailureReason = RequireText(failureReason, nameof(failureReason));
        TransitionTo(RuntimeTaskStatus.Failed, now);
    }

    /// <summary>
    /// Cancels the task.
    /// </summary>
    /// <param name="now">The cancellation timestamp.</param>
    public void Cancel(DateTimeOffset now)
    {
        TransitionTo(RuntimeTaskStatus.Cancelled, now);
        CancelledAt = now;
    }

    /// <summary>
    /// Advances the task to the next bounded iteration.
    /// </summary>
    /// <param name="now">The iteration update timestamp.</param>
    public void AdvanceIteration(DateTimeOffset now)
    {
        if (CurrentIteration >= MaxIterations)
        {
            throw new AegesDomainException($"Task '{Id}' cannot exceed {MaxIterations} iterations.");
        }

        CurrentIteration++;
        UpdatedAt = now;
    }

    private void TransitionTo(RuntimeTaskStatus nextStatus, DateTimeOffset now)
    {
        if (!CanTransition(Status, nextStatus))
        {
            throw new InvalidTaskStatusTransitionException(Status, nextStatus);
        }

        Status = nextStatus;
        UpdatedAt = now;
    }

    private static bool CanTransition(RuntimeTaskStatus currentStatus, RuntimeTaskStatus nextStatus) =>
        currentStatus switch
        {
            RuntimeTaskStatus.Queued => nextStatus is RuntimeTaskStatus.Planning or RuntimeTaskStatus.Cancelled,
            RuntimeTaskStatus.Planning => nextStatus is RuntimeTaskStatus.Running
                or RuntimeTaskStatus.WaitingApproval
                or RuntimeTaskStatus.Failed
                or RuntimeTaskStatus.Cancelled,
            RuntimeTaskStatus.Running => nextStatus is RuntimeTaskStatus.Reviewing
                or RuntimeTaskStatus.WaitingApproval
                or RuntimeTaskStatus.Failed
                or RuntimeTaskStatus.Cancelled,
            RuntimeTaskStatus.Reviewing => nextStatus is RuntimeTaskStatus.Completed
                or RuntimeTaskStatus.Queued
                or RuntimeTaskStatus.Running
                or RuntimeTaskStatus.WaitingApproval
                or RuntimeTaskStatus.Failed
                or RuntimeTaskStatus.Cancelled,
            RuntimeTaskStatus.WaitingApproval => nextStatus is RuntimeTaskStatus.Running
                or RuntimeTaskStatus.Failed
                or RuntimeTaskStatus.Cancelled,
            RuntimeTaskStatus.Cancelled => nextStatus is RuntimeTaskStatus.Queued,
            RuntimeTaskStatus.Completed or RuntimeTaskStatus.Failed => false,
            _ => false,
        };

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value;
    }

    private static int RequirePositive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be greater than zero.");
        }

        return value;
    }
}
