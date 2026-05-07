namespace Aeges.Storage.Sqlite.Entities;

internal sealed class RunnerExecutionRecord
{
    public string Id { get; set; } = string.Empty;

    public string TaskId { get; set; } = string.Empty;

    public string IterationId { get; set; } = string.Empty;

    public string RunnerId { get; set; } = string.Empty;

    public string Command { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public int? ExitCode { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public bool TimedOut { get; set; }

    public bool Cancelled { get; set; }

    public TaskRecord? Task { get; set; }

    public TaskIterationRecord? Iteration { get; set; }
}
