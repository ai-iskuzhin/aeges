using Aeges.Application.Transports;
using Aeges.Core;

namespace Aeges.Application.Tests;

public sealed class TransportCallbackActionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 11, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task TryRegisterAsync_persists_unused_callback_action()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var service = new TransportCallbackActionService(unitOfWork, new FixedClock(Now));
        var action = CreateAction("abc123");

        var registered = await service.TryRegisterAsync(action, CancellationToken.None);
        var stored = await unitOfWork.TransportCallbackActions.GetAsync("telegram", "abc123", CancellationToken.None);

        Assert.True(registered);
        Assert.Same(action, stored);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task TryRegisterAsync_rejects_duplicate_tokens_without_saving()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var service = new TransportCallbackActionService(unitOfWork, new FixedClock(Now));
        await service.TryRegisterAsync(CreateAction("abc123"), CancellationToken.None);

        var registered = await service.TryRegisterAsync(CreateAction("abc123"), CancellationToken.None);

        Assert.False(registered);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ResolveAsync_marks_valid_scoped_action_used()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        var service = new TransportCallbackActionService(unitOfWork, clock);
        await service.TryRegisterAsync(CreateAction("abc123"), CancellationToken.None);
        clock.Now = Now.AddMinutes(5);

        var resolved = await service.ResolveAsync(
            "telegram",
            "chat:1001",
            "abc123",
            CancellationToken.None);

        Assert.NotNull(resolved);
        Assert.Equal(clock.Now, resolved.LastUsedAt);
        Assert.Equal(1, resolved.UseCount);
        Assert.Equal(2, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ResolveAsync_rejects_wrong_scope_without_saving_usage()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var service = new TransportCallbackActionService(unitOfWork, new FixedClock(Now));
        await service.TryRegisterAsync(CreateAction("abc123"), CancellationToken.None);

        var resolved = await service.ResolveAsync(
            "telegram",
            "chat:2002",
            "abc123",
            CancellationToken.None);

        Assert.Null(resolved);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    private static RuntimeTransportCallbackAction CreateAction(string token) =>
        RuntimeTransportCallbackAction.Create(
            token,
            "telegram",
            "chat:1001",
            "callback",
            """{"callbackData":"aeges:menu"}""",
            Now,
            Now.AddDays(1));
}
