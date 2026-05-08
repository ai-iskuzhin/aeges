using Aeges.Core;
using Aeges.Storage.Sqlite;
using Aeges.Storage.Sqlite.Repositories;
using Microsoft.EntityFrameworkCore;

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

    [Fact]
    public async Task RunOnce_claims_one_queued_task_assigned_to_machine()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 05, 08, 08, 00, 00, TimeSpan.Zero));
        var runtime = new LocalAgentRuntime(clock);
        var databasePath = Path.Combine(Path.GetTempPath(), $"aeges-agent-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";

        try
        {
            await SeedProjectMachineAndTaskAsync(connectionString, clock.Now, new MachineId("machine-001"));

            var snapshot = await runtime.RunOnceAsync(
                new AgentRunOptions(
                    connectionString,
                    "machine-001",
                    "local-test",
                    "test-platform",
                    RunnerId: "mock"),
                CancellationToken.None);

            await using var context = new AegesDbContext(AegesDbContextOptions.Create(connectionString));
            var unitOfWork = new SqliteUnitOfWork(context);
            var task = await unitOfWork.Tasks.GetByIdAsync(new TaskId("task-001"), CancellationToken.None);
            var iterations = await unitOfWork.Iterations.ListByTaskAsync(new TaskId("task-001"), CancellationToken.None);

            Assert.Equal("task-001", snapshot.ClaimedTaskId);
            Assert.NotNull(snapshot.CreatedIterationId);
            Assert.Equal(0, snapshot.QueuedTaskCount);
            Assert.NotNull(task);
            Assert.Equal(RuntimeTaskStatus.Planning, task.Status);
            Assert.Equal(1, task.CurrentIteration);
            var iteration = Assert.Single(iterations);
            Assert.Equal(new RunnerId("mock"), iteration.RunnerId);
            Assert.Equal(1, iteration.IterationNumber);
            Assert.Equal(new IterationId(snapshot.CreatedIterationId), iteration.Id);
        }
        finally
        {
            DeleteIfExists(databasePath);
            DeleteIfExists($"{databasePath}-shm");
            DeleteIfExists($"{databasePath}-wal");
        }
    }

    [Fact]
    public async Task RunOnce_does_not_claim_task_assigned_to_another_machine()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 05, 08, 08, 00, 00, TimeSpan.Zero));
        var runtime = new LocalAgentRuntime(clock);
        var databasePath = Path.Combine(Path.GetTempPath(), $"aeges-agent-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";

        try
        {
            await SeedProjectMachineAndTaskAsync(connectionString, clock.Now, new MachineId("other-machine"));

            var snapshot = await runtime.RunOnceAsync(
                new AgentRunOptions(
                    connectionString,
                    "machine-001",
                    "local-test",
                    "test-platform"),
                CancellationToken.None);

            await using var context = new AegesDbContext(AegesDbContextOptions.Create(connectionString));
            var unitOfWork = new SqliteUnitOfWork(context);
            var task = await unitOfWork.Tasks.GetByIdAsync(new TaskId("task-001"), CancellationToken.None);
            var iterations = await unitOfWork.Iterations.ListByTaskAsync(new TaskId("task-001"), CancellationToken.None);

            Assert.Null(snapshot.ClaimedTaskId);
            Assert.Null(snapshot.CreatedIterationId);
            Assert.Equal(1, snapshot.QueuedTaskCount);
            Assert.NotNull(task);
            Assert.Equal(RuntimeTaskStatus.Queued, task.Status);
            Assert.Empty(iterations);
        }
        finally
        {
            DeleteIfExists(databasePath);
            DeleteIfExists($"{databasePath}-shm");
            DeleteIfExists($"{databasePath}-wal");
        }
    }

    [Fact]
    public async Task RunOnce_can_skip_task_claiming()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 05, 08, 08, 00, 00, TimeSpan.Zero));
        var runtime = new LocalAgentRuntime(clock);
        var databasePath = Path.Combine(Path.GetTempPath(), $"aeges-agent-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";

        try
        {
            await SeedProjectMachineAndTaskAsync(connectionString, clock.Now, new MachineId("machine-001"));

            var snapshot = await runtime.RunOnceAsync(
                new AgentRunOptions(
                    connectionString,
                    "machine-001",
                    "local-test",
                    "test-platform",
                    ClaimQueuedTask: false),
                CancellationToken.None);

            Assert.Null(snapshot.ClaimedTaskId);
            Assert.Null(snapshot.CreatedIterationId);
            Assert.Equal(1, snapshot.QueuedTaskCount);
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

    private static async Task SeedProjectMachineAndTaskAsync(
        string connectionString,
        DateTimeOffset now,
        MachineId taskMachineId)
    {
        await using var context = new AegesDbContext(AegesDbContextOptions.Create(connectionString));
        await SqlitePragmas.ApplyAsync(context, CancellationToken.None);
        await context.Database.MigrateAsync(CancellationToken.None);
        var unitOfWork = new SqliteUnitOfWork(context);

        await unitOfWork.Projects.AddAsync(
            RuntimeProject.Create(new ProjectId("project-001"), "Aeges", "/work/aeges", now),
            CancellationToken.None);
        await unitOfWork.Machines.AddAsync(
            RuntimeMachine.Create(taskMachineId, $"machine-{taskMachineId.Value}", "test-platform", now),
            CancellationToken.None);
        await unitOfWork.Tasks.AddAsync(
            RuntimeTask.Create(
                new TaskId("task-001"),
                new ProjectId("project-001"),
                taskMachineId,
                "Queued task",
                "Do governed work.",
                now),
            CancellationToken.None);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);
    }
}
