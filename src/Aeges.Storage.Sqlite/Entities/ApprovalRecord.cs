namespace Aeges.Storage.Sqlite.Entities;

internal sealed class ApprovalRecord
{
    public string Id { get; set; } = string.Empty;

    public string TaskId { get; set; } = string.Empty;

    public string? IterationId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string RequestedAction { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public string? ResolvedBy { get; set; }

    public TaskRecord? Task { get; set; }

    public TaskIterationRecord? Iteration { get; set; }
}
