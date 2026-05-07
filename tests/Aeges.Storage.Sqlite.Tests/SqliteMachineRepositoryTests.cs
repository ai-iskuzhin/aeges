using Aeges.Core;
using Aeges.Storage.Sqlite.Repositories;

namespace Aeges.Storage.Sqlite.Tests;

public sealed class SqliteMachineRepositoryTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 05, 07, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task Add_and_get_round_trips_machine()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteMachineRepository(context);
        var machine = RuntimeMachine.Create(new MachineId("machine-001"), "home-laptop", "macOS arm64", CreatedAt);
        machine.MarkOnline(CreatedAt.AddMinutes(1));

        await repository.AddAsync(machine, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(new MachineId("machine-001"), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(machine.Id, stored.Id);
        Assert.Equal(machine.Name, stored.Name);
        Assert.Equal(machine.Platform, stored.Platform);
        Assert.Equal(machine.Status, stored.Status);
        Assert.Equal(machine.LastSeenAt, stored.LastSeenAt);
        Assert.Equal(machine.CreatedAt, stored.CreatedAt);
        Assert.Equal(machine.UpdatedAt, stored.UpdatedAt);
    }

    [Fact]
    public async Task List_returns_machines_ordered_by_name()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteMachineRepository(context);

        await repository.AddAsync(RuntimeMachine.Create(new MachineId("machine-002"), "zeta", "linux x64", CreatedAt), CancellationToken.None);
        await repository.AddAsync(RuntimeMachine.Create(new MachineId("machine-001"), "alpha", "macOS arm64", CreatedAt), CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var machines = await repository.ListAsync(CancellationToken.None);

        Assert.Collection(
            machines,
            machine => Assert.Equal(new MachineId("machine-001"), machine.Id),
            machine => Assert.Equal(new MachineId("machine-002"), machine.Id));
    }

    [Fact]
    public async Task Update_persists_machine_metadata_and_status()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteMachineRepository(context);
        var machine = RuntimeMachine.Create(new MachineId("machine-001"), "home-laptop", "macOS arm64", CreatedAt);
        await repository.AddAsync(machine, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var updatedAt = CreatedAt.AddMinutes(5);
        machine.Update("build-box", "linux x64", updatedAt);
        machine.MarkBusy(updatedAt.AddMinutes(1));
        await repository.UpdateAsync(machine, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(new MachineId("machine-001"), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal("build-box", stored.Name);
        Assert.Equal("linux x64", stored.Platform);
        Assert.Equal(MachineStatus.Busy, stored.Status);
        Assert.Equal(updatedAt.AddMinutes(1), stored.LastSeenAt);
        Assert.Equal(updatedAt.AddMinutes(1), stored.UpdatedAt);
    }

    [Fact]
    public async Task Get_returns_null_when_machine_does_not_exist()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteMachineRepository(context);

        var stored = await repository.GetByIdAsync(new MachineId("missing"), CancellationToken.None);

        Assert.Null(stored);
    }
}
