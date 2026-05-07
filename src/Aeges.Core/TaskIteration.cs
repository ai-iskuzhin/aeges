namespace Aeges.Core;

/// <summary>
/// Represents one bounded execution attempt within a runtime task.
/// </summary>
public sealed class TaskIteration
{
    private TaskIteration(
        IterationId id,
        TaskId taskId,
        int iterationNumber,
        RunnerId runnerId,
        DateTimeOffset createdAt)
    {
        Id = id;
        TaskId = taskId;
        IterationNumber = RequirePositive(iterationNumber, nameof(iterationNumber));
        RunnerId = runnerId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        Status = TaskIterationStatus.Created;
    }

    /// <summary>
    /// Gets the iteration identifier.
    /// </summary>
    public IterationId Id { get; }

    /// <summary>
    /// Gets the task that owns this iteration.
    /// </summary>
    public TaskId TaskId { get; }

    /// <summary>
    /// Gets the one-based iteration number within the task.
    /// </summary>
    public int IterationNumber { get; }

    /// <summary>
    /// Gets the current iteration lifecycle status.
    /// </summary>
    public TaskIterationStatus Status { get; private set; }

    /// <summary>
    /// Gets the runner assigned to execute this iteration.
    /// </summary>
    public RunnerId RunnerId { get; }

    /// <summary>
    /// Gets the isolated worktree path for this iteration, when assigned.
    /// </summary>
    public string? WorktreePath { get; private set; }

    /// <summary>
    /// Gets the prompt artifact identifier, when attached.
    /// </summary>
    public ArtifactId? PromptArtifactId { get; private set; }

    /// <summary>
    /// Gets the result artifact identifier, when attached.
    /// </summary>
    public ArtifactId? ResultArtifactId { get; private set; }

    /// <summary>
    /// Gets the diff artifact identifier, when attached.
    /// </summary>
    public ArtifactId? DiffArtifactId { get; private set; }

    /// <summary>
    /// Gets the timestamp when the iteration was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the timestamp when the iteration was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Gets the timestamp when runner execution started.
    /// </summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>
    /// Gets the timestamp when the iteration reached a terminal status.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// Gets the reason recorded when the iteration fails.
    /// </summary>
    public string? FailureReason { get; private set; }

    /// <summary>
    /// Creates a new task iteration.
    /// </summary>
    /// <param name="id">The iteration identifier.</param>
    /// <param name="taskId">The task that owns the iteration.</param>
    /// <param name="iterationNumber">The one-based iteration number within the task.</param>
    /// <param name="runnerId">The runner assigned to the iteration.</param>
    /// <param name="createdAt">The iteration creation timestamp.</param>
    /// <returns>A created task iteration.</returns>
    public static TaskIteration Create(
        IterationId id,
        TaskId taskId,
        int iterationNumber,
        RunnerId runnerId,
        DateTimeOffset createdAt) =>
        new(id, taskId, iterationNumber, runnerId, createdAt);

    /// <summary>
    /// Rehydrates a task iteration from durable storage.
    /// </summary>
    /// <param name="id">The iteration identifier.</param>
    /// <param name="taskId">The task that owns the iteration.</param>
    /// <param name="iterationNumber">The one-based iteration number within the task.</param>
    /// <param name="status">The current iteration lifecycle status.</param>
    /// <param name="runnerId">The runner assigned to the iteration.</param>
    /// <param name="worktreePath">The isolated worktree path for this iteration, when assigned.</param>
    /// <param name="promptArtifactId">The prompt artifact identifier, when attached.</param>
    /// <param name="resultArtifactId">The result artifact identifier, when attached.</param>
    /// <param name="diffArtifactId">The diff artifact identifier, when attached.</param>
    /// <param name="createdAt">The iteration creation timestamp.</param>
    /// <param name="updatedAt">The last update timestamp.</param>
    /// <param name="startedAt">The timestamp when runner execution started.</param>
    /// <param name="completedAt">The timestamp when the iteration reached a terminal status.</param>
    /// <param name="failureReason">The reason recorded when the iteration failed.</param>
    /// <returns>A rehydrated task iteration.</returns>
    public static TaskIteration Rehydrate(
        IterationId id,
        TaskId taskId,
        int iterationNumber,
        TaskIterationStatus status,
        RunnerId runnerId,
        string? worktreePath,
        ArtifactId? promptArtifactId,
        ArtifactId? resultArtifactId,
        ArtifactId? diffArtifactId,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        string? failureReason)
    {
        var iteration = new TaskIteration(id, taskId, iterationNumber, runnerId, createdAt)
        {
            Status = status,
            WorktreePath = worktreePath is null ? null : RequireText(worktreePath, nameof(worktreePath)),
            PromptArtifactId = promptArtifactId,
            ResultArtifactId = resultArtifactId,
            DiffArtifactId = diffArtifactId,
            UpdatedAt = updatedAt,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            FailureReason = failureReason,
        };

        return iteration;
    }

    /// <summary>
    /// Assigns the isolated worktree path used by this iteration.
    /// </summary>
    /// <param name="worktreePath">The worktree path.</param>
    /// <param name="now">The update timestamp.</param>
    public void AssignWorktree(string worktreePath, DateTimeOffset now)
    {
        WorktreePath = RequireText(worktreePath, nameof(worktreePath));
        UpdatedAt = now;
    }

    /// <summary>
    /// Attaches the prompt artifact used for this iteration.
    /// </summary>
    /// <param name="artifactId">The prompt artifact identifier.</param>
    /// <param name="now">The update timestamp.</param>
    public void AttachPromptArtifact(ArtifactId artifactId, DateTimeOffset now)
    {
        PromptArtifactId = artifactId;
        UpdatedAt = now;
    }

    /// <summary>
    /// Attaches the result artifact produced by this iteration.
    /// </summary>
    /// <param name="artifactId">The result artifact identifier.</param>
    /// <param name="now">The update timestamp.</param>
    public void AttachResultArtifact(ArtifactId artifactId, DateTimeOffset now)
    {
        ResultArtifactId = artifactId;
        UpdatedAt = now;
    }

    /// <summary>
    /// Attaches the diff artifact produced by this iteration.
    /// </summary>
    /// <param name="artifactId">The diff artifact identifier.</param>
    /// <param name="now">The update timestamp.</param>
    public void AttachDiffArtifact(ArtifactId artifactId, DateTimeOffset now)
    {
        DiffArtifactId = artifactId;
        UpdatedAt = now;
    }

    /// <summary>
    /// Moves the iteration into runner execution.
    /// </summary>
    /// <param name="now">The transition timestamp.</param>
    public void StartRunning(DateTimeOffset now)
    {
        TransitionTo(TaskIterationStatus.Running, now);
        StartedAt ??= now;
    }

    /// <summary>
    /// Moves the iteration into artifact review.
    /// </summary>
    /// <param name="now">The transition timestamp.</param>
    public void StartReview(DateTimeOffset now) => TransitionTo(TaskIterationStatus.Reviewing, now);

    /// <summary>
    /// Pauses the iteration until a required approval is resolved.
    /// </summary>
    /// <param name="now">The transition timestamp.</param>
    public void WaitForApproval(DateTimeOffset now) => TransitionTo(TaskIterationStatus.WaitingApproval, now);

    /// <summary>
    /// Marks the iteration as successfully completed.
    /// </summary>
    /// <param name="now">The completion timestamp.</param>
    public void Complete(DateTimeOffset now)
    {
        TransitionTo(TaskIterationStatus.Completed, now);
        CompletedAt = now;
    }

    /// <summary>
    /// Marks the iteration as failed with a durable reason.
    /// </summary>
    /// <param name="failureReason">The reason the iteration failed.</param>
    /// <param name="now">The failure timestamp.</param>
    public void Fail(string failureReason, DateTimeOffset now)
    {
        FailureReason = RequireText(failureReason, nameof(failureReason));
        TransitionTo(TaskIterationStatus.Failed, now);
        CompletedAt = now;
    }

    /// <summary>
    /// Cancels the iteration.
    /// </summary>
    /// <param name="now">The cancellation timestamp.</param>
    public void Cancel(DateTimeOffset now)
    {
        TransitionTo(TaskIterationStatus.Cancelled, now);
        CompletedAt = now;
    }

    private void TransitionTo(TaskIterationStatus nextStatus, DateTimeOffset now)
    {
        if (!CanTransition(Status, nextStatus))
        {
            throw new InvalidTaskIterationStatusTransitionException(Status, nextStatus);
        }

        Status = nextStatus;
        UpdatedAt = now;
    }

    private static bool CanTransition(TaskIterationStatus currentStatus, TaskIterationStatus nextStatus) =>
        currentStatus switch
        {
            TaskIterationStatus.Created => nextStatus is TaskIterationStatus.Running or TaskIterationStatus.Cancelled,
            TaskIterationStatus.Running => nextStatus is TaskIterationStatus.Reviewing
                or TaskIterationStatus.WaitingApproval
                or TaskIterationStatus.Failed
                or TaskIterationStatus.Cancelled,
            TaskIterationStatus.Reviewing => nextStatus is TaskIterationStatus.Completed
                or TaskIterationStatus.Running
                or TaskIterationStatus.WaitingApproval
                or TaskIterationStatus.Failed
                or TaskIterationStatus.Cancelled,
            TaskIterationStatus.WaitingApproval => nextStatus is TaskIterationStatus.Running
                or TaskIterationStatus.Failed
                or TaskIterationStatus.Cancelled,
            TaskIterationStatus.Completed or TaskIterationStatus.Failed or TaskIterationStatus.Cancelled => false,
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
