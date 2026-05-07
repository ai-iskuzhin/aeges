namespace Aeges.Storage.Sqlite.Entities;

internal sealed class TaskIterationRecord
{
    public string Id { get; set; } = string.Empty;

    public string TaskId { get; set; } = string.Empty;

    public int IterationNumber { get; set; }

    public string Status { get; set; } = string.Empty;

    public string RunnerId { get; set; } = string.Empty;

    public string? WorktreePath { get; set; }

    public string? PromptArtifactId { get; set; }

    public string? ResultArtifactId { get; set; }

    public string? DiffArtifactId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? FailureReason { get; set; }

    public TaskRecord? Task { get; set; }

    public ICollection<ArtifactRecord> Artifacts { get; } = [];

    public ICollection<ApprovalRecord> Approvals { get; } = [];

    public ICollection<RuntimeEventRecord> RuntimeEvents { get; } = [];

    public ICollection<RunnerExecutionRecord> RunnerExecutions { get; } = [];
}
