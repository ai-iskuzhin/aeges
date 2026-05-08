using Aeges.Core;

namespace Aeges.Storage.Sqlite.Entities;

/// <summary>
/// EF Core persistence record for a durable runtime task.
/// </summary>
internal sealed class TaskRecord
{
    /// <summary>
    /// Gets or sets the task identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the owning project identifier.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the assigned machine identifier.
    /// </summary>
    public string MachineId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable task title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the durable task goal.
    /// </summary>
    public string Goal { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the task lifecycle status.
    /// </summary>
    public RuntimeTaskStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the task scheduling priority.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of allowed task iterations.
    /// </summary>
    public int MaxIterations { get; set; }

    /// <summary>
    /// Gets or sets the current completed or active iteration number.
    /// </summary>
    public int CurrentIteration { get; set; }

    /// <summary>
    /// Gets or sets the task creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when task execution started.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the successful completion timestamp.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the cancellation timestamp.
    /// </summary>
    public DateTimeOffset? CancelledAt { get; set; }

    /// <summary>
    /// Gets or sets the durable failure reason, when the task failed.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Gets or sets the owning project navigation.
    /// </summary>
    public ProjectRecord? Project { get; set; }

    /// <summary>
    /// Gets or sets the assigned machine navigation.
    /// </summary>
    public MachineRecord? Machine { get; set; }

    /// <summary>
    /// Gets the task iterations.
    /// </summary>
    public ICollection<TaskIterationRecord> Iterations { get; } = [];

    /// <summary>
    /// Gets the artifacts belonging to this task.
    /// </summary>
    public ICollection<ArtifactRecord> Artifacts { get; } = [];

    /// <summary>
    /// Gets the approval requests belonging to this task.
    /// </summary>
    public ICollection<ApprovalRecord> Approvals { get; } = [];

    /// <summary>
    /// Gets the locks held or previously held for this task.
    /// </summary>
    public ICollection<LockRecord> Locks { get; } = [];

    /// <summary>
    /// Gets the runtime events associated with this task.
    /// </summary>
    public ICollection<RuntimeEventRecord> RuntimeEvents { get; } = [];

    /// <summary>
    /// Gets the runner executions associated with this task.
    /// </summary>
    public ICollection<RunnerExecutionRecord> RunnerExecutions { get; } = [];
}
