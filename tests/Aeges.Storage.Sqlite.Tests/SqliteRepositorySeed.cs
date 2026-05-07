using Aeges.Core;
using Aeges.Storage.Sqlite.Repositories;

namespace Aeges.Storage.Sqlite.Tests;

internal static class SqliteRepositorySeed
{
    public static readonly DateTimeOffset CreatedAt = new(2026, 05, 07, 12, 00, 00, TimeSpan.Zero);

    public static async Task SeedProjectMachineAndTaskAsync(AegesDbContext context)
    {
        var projects = new SqliteProjectRepository(context);
        var machines = new SqliteMachineRepository(context);
        var tasks = new SqliteTaskRepository(context);

        await projects.AddAsync(
            RuntimeProject.Create(new ProjectId("project-001"), "Aeges", "/work/aeges", CreatedAt),
            CancellationToken.None);
        await machines.AddAsync(
            RuntimeMachine.Create(new MachineId("machine-001"), "home-laptop", "macOS arm64", CreatedAt),
            CancellationToken.None);
        await tasks.AddAsync(CreateTask(new TaskId("task-001")), CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    public static RuntimeTask CreateTask(TaskId id) =>
        RuntimeTask.Create(
            id,
            new ProjectId("project-001"),
            new MachineId("machine-001"),
            $"Task {id.Value}",
            "Do governed work.",
            CreatedAt);
}
