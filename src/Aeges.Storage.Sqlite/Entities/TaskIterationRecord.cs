using Aeges.Core;

namespace Aeges.Storage.Sqlite.Entities;

/// <summary>
/// EF Core persistence record for a bounded task iteration.
/// </summary>
internal sealed class TaskIterationRecord
{
    /// <summary>
    /// Gets or sets the iteration identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the owning task identifier.
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the one-based iteration number within the task.
    /// </summary>
    public int IterationNumber { get; set; }

    /// <summary>
    /// Gets or sets the iteration lifecycle status.
    /// </summary>
    public TaskIterationStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the runner assigned to this iteration.
    /// </summary>
    public string RunnerId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the isolated worktree path used by this iteration.
    /// </summary>
    public string? WorktreePath { get; set; }

    /// <summary>
    /// Gets or sets the prompt artifact identifier.
    /// </summary>
    public string? PromptArtifactId { get; set; }

    /// <summary>
    /// Gets or sets the result artifact identifier.
    /// </summary>
    public string? ResultArtifactId { get; set; }

    /// <summary>
    /// Gets or sets the diff artifact identifier.
    /// </summary>
    public string? DiffArtifactId { get; set; }

    /// <summary>
    /// Gets or sets the iteration creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the runner execution start timestamp.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the terminal completion timestamp.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the durable failure reason, when the iteration failed.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Gets or sets the owning task navigation.
    /// </summary>
    public TaskRecord? Task { get; set; }

    /// <summary>
    /// Gets the artifact metadata associated with this iteration.
    /// </summary>
    public ICollection<ArtifactRecord> Artifacts { get; } = [];

    /// <summary>
    /// Gets the approval requests associated with this iteration.
    /// </summary>
    public ICollection<ApprovalRecord> Approvals { get; } = [];

    /// <summary>
    /// Gets the runtime events associated with this iteration.
    /// </summary>
    public ICollection<RuntimeEventRecord> RuntimeEvents { get; } = [];

    /// <summary>
    /// Gets the runner executions associated with this iteration.
    /// </summary>
    public ICollection<RunnerExecutionRecord> RunnerExecutions { get; } = [];
}
