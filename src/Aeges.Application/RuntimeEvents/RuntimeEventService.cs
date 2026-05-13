using Aeges.Core;
using Aeges.Storage;

namespace Aeges.Application.RuntimeEvents;

/// <summary>
/// Coordinates durable runtime event recording and lookup use cases.
/// </summary>
public sealed class RuntimeEventService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeEventService"/> class.
    /// </summary>
    /// <param name="unitOfWork">The persistence unit of work.</param>
    /// <param name="clock">The deterministic application clock.</param>
    public RuntimeEventService(IUnitOfWork unitOfWork, IClock clock)
    {
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    /// <summary>
    /// Records a runtime event.
    /// </summary>
    /// <param name="request">The event recording request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The recorded runtime event, or an expected failure.</returns>
    public async Task<ApplicationResult<RuntimeEvent>> RecordAsync(
        RecordRuntimeEventRequest request,
        CancellationToken cancellationToken)
    {
        RuntimeEvent runtimeEvent;

        try
        {
            runtimeEvent = RuntimeEvent.Create(
                request.RuntimeEventId ?? RuntimeEventId.New(),
                request.TaskId,
                request.IterationId,
                request.MachineId,
                request.EventType,
                request.Message,
                request.PayloadJson,
                clock.Now);
        }
        catch (ArgumentException exception)
        {
            return ApplicationResult<RuntimeEvent>.Failure("invalid_runtime_event", exception.Message);
        }

        await unitOfWork.RuntimeEvents.AddAsync(runtimeEvent, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RuntimeEvent>.Success(runtimeEvent);
    }

    /// <summary>
    /// Lists recent runtime events belonging to a task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="limit">The maximum number of most recent events to return.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task runtime events in chronological order.</returns>
    public async Task<IReadOnlyList<RuntimeEvent>> ListByTaskAsync(
        TaskId taskId,
        int limit,
        CancellationToken cancellationToken) =>
        await unitOfWork.RuntimeEvents.ListByTaskAsync(taskId, limit, cancellationToken);
}
