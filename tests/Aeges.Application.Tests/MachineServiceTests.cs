using Aeges.Application.Machines;
using Aeges.Core;

namespace Aeges.Application.Tests;

public sealed class MachineServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 07, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task RegisterAsync_creates_machine()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var service = new MachineService(unitOfWork, new FixedClock(Now));

        var result = await service.RegisterAsync(
            new RegisterMachineRequest("home-laptop", "macOS arm64", new MachineId("machine-001")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(new MachineId("machine-001"), result.Value.Id);
        Assert.Equal(MachineStatus.Offline, result.Value.Status);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HeartbeatAsync_marks_machine_online()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        var service = new MachineService(unitOfWork, clock);
        await service.RegisterAsync(
            new RegisterMachineRequest("home-laptop", "macOS arm64", new MachineId("machine-001")),
            CancellationToken.None);
        clock.Now = Now.AddMinutes(1);

        var result = await service.HeartbeatAsync(new MachineId("machine-001"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(MachineStatus.Online, result.Value.Status);
        Assert.Equal(Now.AddMinutes(1), result.Value.LastSeenAt);
    }

    [Fact]
    public async Task HeartbeatAsync_returns_failure_when_machine_is_missing()
    {
        var service = new MachineService(new InMemoryUnitOfWork(), new FixedClock(Now));

        var result = await service.HeartbeatAsync(new MachineId("missing"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("machine_not_found", result.Error?.Code);
    }
}
