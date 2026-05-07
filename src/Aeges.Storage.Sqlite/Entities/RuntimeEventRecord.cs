namespace Aeges.Storage.Sqlite.Entities;

internal sealed class RuntimeEventRecord
{
    public string Id { get; set; } = string.Empty;

    public string? TaskId { get; set; }

    public string? IterationId { get; set; }

    public string? MachineId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? PayloadJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public TaskRecord? Task { get; set; }

    public TaskIterationRecord? Iteration { get; set; }

    public MachineRecord? Machine { get; set; }
}
