namespace Aeges.Storage.Sqlite.Entities;

/// <summary>
/// EF Core persistence record for an auditable runtime event.
/// </summary>
internal sealed class RuntimeEventRecord
{
    /// <summary>
    /// Gets or sets the runtime event identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the related task identifier, when the event is task-scoped.
    /// </summary>
    public string? TaskId { get; set; }

    /// <summary>
    /// Gets or sets the related iteration identifier, when the event is iteration-scoped.
    /// </summary>
    public string? IterationId { get; set; }

    /// <summary>
    /// Gets or sets the related machine identifier, when the event is machine-scoped.
    /// </summary>
    public string? MachineId { get; set; }

    /// <summary>
    /// Gets or sets the stable runtime event type.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable event message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional structured event payload JSON.
    /// </summary>
    public string? PayloadJson { get; set; }

    /// <summary>
    /// Gets or sets the event creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the related task navigation.
    /// </summary>
    public TaskRecord? Task { get; set; }

    /// <summary>
    /// Gets or sets the related iteration navigation.
    /// </summary>
    public TaskIterationRecord? Iteration { get; set; }

    /// <summary>
    /// Gets or sets the related machine navigation.
    /// </summary>
    public MachineRecord? Machine { get; set; }
}
