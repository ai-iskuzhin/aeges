using Aeges.Core;

namespace Aeges.Core.Tests;

public sealed class RuntimeMachineTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 05, 07, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Create_initializes_offline_machine()
    {
        var machine = CreateMachine();

        Assert.Equal(new MachineId("machine-001"), machine.Id);
        Assert.Equal("home-laptop", machine.Name);
        Assert.Equal("macOS arm64", machine.Platform);
        Assert.Equal(MachineStatus.Offline, machine.Status);
        Assert.Null(machine.LastSeenAt);
        Assert.Equal(CreatedAt, machine.CreatedAt);
        Assert.Equal(CreatedAt, machine.UpdatedAt);
    }

    [Fact]
    public void Rehydrate_restores_machine_state()
    {
        var lastSeenAt = CreatedAt.AddMinutes(4);
        var updatedAt = CreatedAt.AddMinutes(5);

        var machine = RuntimeMachine.Rehydrate(
            new MachineId("machine-001"),
            "home-laptop",
            "macOS arm64",
            MachineStatus.Busy,
            lastSeenAt,
            CreatedAt,
            updatedAt);

        Assert.Equal(new MachineId("machine-001"), machine.Id);
        Assert.Equal("home-laptop", machine.Name);
        Assert.Equal("macOS arm64", machine.Platform);
        Assert.Equal(MachineStatus.Busy, machine.Status);
        Assert.Equal(lastSeenAt, machine.LastSeenAt);
        Assert.Equal(CreatedAt, machine.CreatedAt);
        Assert.Equal(updatedAt, machine.UpdatedAt);
    }

    [Fact]
    public void Machine_status_updates_track_timestamps()
    {
        var machine = CreateMachine();
        var onlineAt = CreatedAt.AddMinutes(1);
        var busyAt = CreatedAt.AddMinutes(2);
        var offlineAt = CreatedAt.AddMinutes(3);

        machine.MarkOnline(onlineAt);
        machine.MarkBusy(busyAt);
        machine.MarkOffline(offlineAt);

        Assert.Equal(MachineStatus.Offline, machine.Status);
        Assert.Equal(busyAt, machine.LastSeenAt);
        Assert.Equal(offlineAt, machine.UpdatedAt);
    }

    [Fact]
    public void Update_changes_machine_metadata()
    {
        var machine = CreateMachine();
        var updatedAt = CreatedAt.AddMinutes(1);

        machine.Update("build-box", "linux x64", updatedAt);

        Assert.Equal("build-box", machine.Name);
        Assert.Equal("linux x64", machine.Platform);
        Assert.Equal(updatedAt, machine.UpdatedAt);
    }

    [Theory]
    [InlineData(MachineStatus.Offline, "offline")]
    [InlineData(MachineStatus.Online, "online")]
    [InlineData(MachineStatus.Busy, "busy")]
    public void Status_round_trips_storage_values(MachineStatus status, string storageValue)
    {
        Assert.Equal(storageValue, status.ToStorageValue());
        Assert.Equal(status, MachineStatusExtensions.FromStorageValue(storageValue));
    }

    private static RuntimeMachine CreateMachine() =>
        RuntimeMachine.Create(new MachineId("machine-001"), "home-laptop", "macOS arm64", CreatedAt);
}
