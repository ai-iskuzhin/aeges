namespace Aeges.Core;

/// <summary>
/// Represents a durable, auditable event emitted by the runtime or one of its governed workers.
/// </summary>
public sealed class RuntimeEvent
{
    private RuntimeEvent(
        RuntimeEventId id,
        TaskId? taskId,
        IterationId? iterationId,
        MachineId? machineId,
        string eventType,
        string message,
        string? payloadJson,
        DateTimeOffset createdAt)
    {
        Id = id;
        TaskId = taskId;
        IterationId = iterationId;
        MachineId = machineId;
        EventType = RequireText(eventType, nameof(eventType));
        Message = RequireText(message, nameof(message));
        PayloadJson = RequireOptionalText(payloadJson, nameof(payloadJson));
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Gets the runtime event identifier.
    /// </summary>
    public RuntimeEventId Id { get; }

    /// <summary>
    /// Gets the related task identifier, when the event is task-scoped.
    /// </summary>
    public TaskId? TaskId { get; }

    /// <summary>
    /// Gets the related iteration identifier, when the event is iteration-scoped.
    /// </summary>
    public IterationId? IterationId { get; }

    /// <summary>
    /// Gets the related machine identifier, when the event is machine-scoped.
    /// </summary>
    public MachineId? MachineId { get; }

    /// <summary>
    /// Gets the stable event type.
    /// </summary>
    public string EventType { get; }

    /// <summary>
    /// Gets the human-readable event message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets optional structured event payload JSON.
    /// </summary>
    public string? PayloadJson { get; }

    /// <summary>
    /// Gets the event creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Creates a runtime event.
    /// </summary>
    /// <param name="id">The event identifier.</param>
    /// <param name="taskId">The related task identifier, when task-scoped.</param>
    /// <param name="iterationId">The related iteration identifier, when iteration-scoped.</param>
    /// <param name="machineId">The related machine identifier, when machine-scoped.</param>
    /// <param name="eventType">The stable event type.</param>
    /// <param name="message">The human-readable event message.</param>
    /// <param name="payloadJson">Optional structured event payload JSON.</param>
    /// <param name="createdAt">The event creation timestamp.</param>
    /// <returns>A runtime event.</returns>
    public static RuntimeEvent Create(
        RuntimeEventId id,
        TaskId? taskId,
        IterationId? iterationId,
        MachineId? machineId,
        string eventType,
        string message,
        string? payloadJson,
        DateTimeOffset createdAt) =>
        new(id, taskId, iterationId, machineId, eventType, message, payloadJson, createdAt);

    /// <summary>
    /// Rehydrates a runtime event from durable storage.
    /// </summary>
    /// <param name="id">The event identifier.</param>
    /// <param name="taskId">The related task identifier, when task-scoped.</param>
    /// <param name="iterationId">The related iteration identifier, when iteration-scoped.</param>
    /// <param name="machineId">The related machine identifier, when machine-scoped.</param>
    /// <param name="eventType">The stable event type.</param>
    /// <param name="message">The human-readable event message.</param>
    /// <param name="payloadJson">Optional structured event payload JSON.</param>
    /// <param name="createdAt">The event creation timestamp.</param>
    /// <returns>A rehydrated runtime event.</returns>
    public static RuntimeEvent Rehydrate(
        RuntimeEventId id,
        TaskId? taskId,
        IterationId? iterationId,
        MachineId? machineId,
        string eventType,
        string message,
        string? payloadJson,
        DateTimeOffset createdAt) =>
        new(id, taskId, iterationId, machineId, eventType, message, payloadJson, createdAt);

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value;
    }

    private static string? RequireOptionalText(string? value, string parameterName)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty when supplied.", parameterName);
        }

        return value;
    }
}
