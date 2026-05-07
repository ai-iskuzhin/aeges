using Aeges.Core;

namespace Aeges.Storage.Sqlite.Entities;

internal sealed class TaskRecord
{
    public string Id { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string MachineId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Goal { get; set; } = string.Empty;

    public RuntimeTaskStatus Status { get; set; }

    public int Priority { get; set; }

    public int MaxIterations { get; set; }

    public int CurrentIteration { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public string? FailureReason { get; set; }

    public ProjectRecord? Project { get; set; }

    public MachineRecord? Machine { get; set; }

    public ICollection<TaskIterationRecord> Iterations { get; } = [];

    public ICollection<ArtifactRecord> Artifacts { get; } = [];

    public ICollection<ApprovalRecord> Approvals { get; } = [];

    public ICollection<LockRecord> Locks { get; } = [];

    public ICollection<RuntimeEventRecord> RuntimeEvents { get; } = [];

    public ICollection<RunnerExecutionRecord> RunnerExecutions { get; } = [];
}
