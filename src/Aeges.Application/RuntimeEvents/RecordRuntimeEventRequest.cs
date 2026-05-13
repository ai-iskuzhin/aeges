using Aeges.Core;

namespace Aeges.Application.RuntimeEvents;

/// <summary>
/// Describes an auditable runtime event that should be persisted.
/// </summary>
/// <param name="TaskId">The related task identifier, when task-scoped.</param>
/// <param name="IterationId">The related iteration identifier, when iteration-scoped.</param>
/// <param name="MachineId">The related machine identifier, when machine-scoped.</param>
/// <param name="EventType">The stable event type.</param>
/// <param name="Message">The human-readable event message.</param>
/// <param name="PayloadJson">Optional structured event payload JSON.</param>
/// <param name="RuntimeEventId">The optional explicit event identifier.</param>
public sealed record RecordRuntimeEventRequest(
    TaskId? TaskId,
    IterationId? IterationId,
    MachineId? MachineId,
    string EventType,
    string Message,
    string? PayloadJson = null,
    RuntimeEventId? RuntimeEventId = null);
