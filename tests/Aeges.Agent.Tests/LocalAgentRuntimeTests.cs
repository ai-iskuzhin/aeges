using Aeges.Core;
using Aeges.Storage.Sqlite;
using Aeges.Storage.Sqlite.Repositories;

namespace Aeges.Agent.Tests;

public sealed class LocalAgentRuntimeTests
{
    [Fact]
    public async Task RunOnce_registers_machine_and_returns_queue_snapshot()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 05, 08, 08, 00, 00, TimeSpan.Zero));
        var runtime = new LocalAgentRuntime(clock);
        var databasePath = Path.Combine(Path.GetTempPath(), $"aeges-agent-{Guid.NewGuid():N}.db");

        try
        {
            var snapshot = await runtime.RunOnceAsync(
                new AgentRunOptions(
                    $"Data Source={databasePath}",
                    "machine-001",
                    "local-test",
                    "test-platform"),
                CancellationToken.None);

            Assert.Equal("machine-001", snapshot.MachineId);
            Assert.EndsWith(Path.GetFileName(databasePath), snapshot.DatabasePath, StringComparison.Ordinal);
            Assert.Equal(clock.Now, snapshot.HeartbeatAt);
            Assert.Equal(0, snapshot.QueuedTaskCount);

            await using var context = new AegesDbContext(AegesDbContextOptions.Create($"Data Source={databasePath}"));
            var machine = await new SqliteMachineRepository(context)
                .GetByIdAsync(new MachineId("machine-001"), CancellationToken.None);

            Assert.NotNull(machine);
            Assert.Equal(MachineStatus.Online, machine.Status);
            Assert.Equal(clock.Now, machine.LastSeenAt);
        }
        finally
        {
            DeleteIfExists(databasePath);
            DeleteIfExists($"{databasePath}-shm");
            DeleteIfExists($"{databasePath}-wal");
        }
    }

    [Fact]
    public async Task RunOnce_updates_existing_machine_heartbeat()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 05, 08, 08, 00, 00, TimeSpan.Zero));
        var runtime = new LocalAgentRuntime(clock);
        var databasePath = Path.Combine(Path.GetTempPath(), $"aeges-agent-{Guid.NewGuid():N}.db");
        var options = new AgentRunOptions(
            $"Data Source={databasePath}",
            "machine-001",
            "local-test",
            "test-platform");

        try
        {
            await runtime.RunOnceAsync(options, CancellationToken.None);
            clock.Now = clock.Now.AddMinutes(1);

            var snapshot = await runtime.RunOnceAsync(options, CancellationToken.None);

            await using var context = new AegesDbContext(AegesDbContextOptions.Create($"Data Source={databasePath}"));
            var machines = await new SqliteMachineRepository(context).ListAsync(CancellationToken.None);

            Assert.Single(machines);
            Assert.Equal(clock.Now, snapshot.HeartbeatAt);
            Assert.Equal(clock.Now, machines[0].LastSeenAt);
        }
        finally
        {
            DeleteIfExists(databasePath);
            DeleteIfExists($"{databasePath}-shm");
            DeleteIfExists($"{databasePath}-wal");
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
