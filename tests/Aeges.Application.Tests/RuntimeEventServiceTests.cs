using Aeges.Application.RuntimeEvents;
using Aeges.Core;

namespace Aeges.Application.Tests;

public sealed class RuntimeEventServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 13, 10, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task RecordAsync_persists_task_runtime_event()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var service = new RuntimeEventService(unitOfWork, new FixedClock(Now));
        var taskId = new TaskId("task-001");

        var result = await service.RecordAsync(
            new RecordRuntimeEventRequest(
                taskId,
                new IterationId("iteration-001"),
                new MachineId("machine-001"),
                "runner.message",
                "Runner produced a short update.",
                """{"source":"test"}""",
                new RuntimeEventId("runtime-event-001")),
            CancellationToken.None);

        var events = await service.ListByTaskAsync(taskId, 10, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var runtimeEvent = Assert.Single(events);
        Assert.Equal(new RuntimeEventId("runtime-event-001"), runtimeEvent.Id);
        Assert.Equal("runner.message", runtimeEvent.EventType);
        Assert.Equal("Runner produced a short update.", runtimeEvent.Message);
        Assert.Equal("""{"source":"test"}""", runtimeEvent.PayloadJson);
        Assert.Equal(Now, runtimeEvent.CreatedAt);
    }

    [Fact]
    public async Task RecordAsync_rejects_empty_event_type()
    {
        var service = new RuntimeEventService(new InMemoryUnitOfWork(), new FixedClock(Now));

        var result = await service.RecordAsync(
            new RecordRuntimeEventRequest(
                new TaskId("task-001"),
                null,
                null,
                " ",
                "Message."),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_runtime_event", result.Error!.Code);
    }
}
