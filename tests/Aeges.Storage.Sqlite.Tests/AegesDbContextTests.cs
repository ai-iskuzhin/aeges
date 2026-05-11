using Aeges.Core;
using Aeges.Storage.Sqlite;
using Aeges.Storage.Sqlite.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Aeges.Storage.Sqlite.Tests;

public sealed class AegesDbContextTests
{
    [Fact]
    public async Task Migrate_creates_mvp_schema_in_temporary_sqlite_file()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"aeges-{Guid.NewGuid():N}.db");

        try
        {
            await using var context = new AegesDbContext(
                AegesDbContextOptions.Create($"Data Source={databasePath}"));

            await SqlitePragmas.ApplyAsync(context, CancellationToken.None);
            await context.Database.MigrateAsync(CancellationToken.None);

            var tableNames = await ReadTableNamesAsync(context, CancellationToken.None);
            var expectedTableNames = new HashSet<string>
            {
                "projects",
                "tasks",
                "task_iterations",
                "artifacts",
                "approvals",
                "locks",
                "runtime_events",
                "runner_executions",
                "machines",
                "talk_sessions",
                "talk_messages",
                "transport_callback_actions",
            };

            Assert.True(expectedTableNames.IsSubsetOf(tableNames));
            Assert.Contains("__EFMigrationsHistory", tableNames);
            Assert.Equal("1", await ExecuteScalarTextAsync(context, "PRAGMA foreign_keys;", CancellationToken.None));
            Assert.Equal("5000", await ExecuteScalarTextAsync(context, "PRAGMA busy_timeout;", CancellationToken.None));
        }
        finally
        {
            DeleteIfExists(databasePath);
            DeleteIfExists($"{databasePath}-shm");
            DeleteIfExists($"{databasePath}-wal");
        }
    }

    [Fact]
    public void Model_maps_expected_table_names()
    {
        using var context = new AegesDbContext(
            AegesDbContextOptions.Create("Data Source=:memory:"));

        var tableNames = context.Model
            .GetEntityTypes()
            .Select(entityType => entityType.GetTableName())
            .Where(tableName => tableName is not null)
            .Select(tableName => tableName!)
            .ToHashSet(StringComparer.Ordinal);

        var expectedTableNames = new HashSet<string>
        {
            "projects",
            "tasks",
            "task_iterations",
            "artifacts",
            "approvals",
            "locks",
            "runtime_events",
            "runner_executions",
            "machines",
            "talk_sessions",
            "talk_messages",
            "transport_callback_actions",
        };

        Assert.True(expectedTableNames.SetEquals(tableNames));
    }

    [Fact]
    public async Task Enum_catalog_values_are_stored_as_stable_text()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SqliteRepositorySeed.SeedProjectMachineAndTaskAsync(context);

        var iterations = new SqliteIterationRepository(context);
        var artifacts = new SqliteArtifactRepository(context);
        var approvals = new SqliteApprovalRepository(context);
        var machines = new SqliteMachineRepository(context);
        var now = SqliteRepositorySeed.CreatedAt.AddMinutes(1);

        await iterations.AddAsync(
            TaskIteration.Create(
                new IterationId("iteration-001"),
                new TaskId("task-001"),
                1,
                new RunnerId("codex"),
                now),
            CancellationToken.None);
        await artifacts.AddAsync(
            new RuntimeArtifact(
                new ArtifactId("artifact-001"),
                new TaskId("task-001"),
                new IterationId("iteration-001"),
                ArtifactType.StdoutLog,
                "tasks/task-001/stdout.log",
                now),
            CancellationToken.None);
        await approvals.AddAsync(
            ApprovalRequest.Create(
                new ApprovalId("approval-001"),
                new TaskId("task-001"),
                new IterationId("iteration-001"),
                "Dependency change requires approval.",
                "Modify package catalog.",
                now),
            CancellationToken.None);

        var machine = await machines.GetByIdAsync(new MachineId("machine-001"), CancellationToken.None);
        machine!.MarkBusy(now);
        await machines.UpdateAsync(machine, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        Assert.Equal(
            "queued",
            await ExecuteScalarTextAsync(context, "SELECT status FROM tasks WHERE id = 'task-001';", CancellationToken.None));
        Assert.Equal(
            "created",
            await ExecuteScalarTextAsync(
                context,
                "SELECT status FROM task_iterations WHERE id = 'iteration-001';",
                CancellationToken.None));
        Assert.Equal(
            "stdout_log",
            await ExecuteScalarTextAsync(
                context,
                "SELECT type FROM artifacts WHERE id = 'artifact-001';",
                CancellationToken.None));
        Assert.Equal(
            "pending",
            await ExecuteScalarTextAsync(
                context,
                "SELECT status FROM approvals WHERE id = 'approval-001';",
                CancellationToken.None));
        Assert.Equal(
            "busy",
            await ExecuteScalarTextAsync(context, "SELECT status FROM machines WHERE id = 'machine-001';", CancellationToken.None));
    }

    private static async Task<HashSet<string>> ReadTableNamesAsync(
        AegesDbContext context,
        CancellationToken cancellationToken)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";

        var tableNames = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            tableNames.Add(reader.GetString(0));
        }

        return tableNames;
    }

    private static async Task<string?> ExecuteScalarTextAsync(
        AegesDbContext context,
        string commandText,
        CancellationToken cancellationToken)
    {
        if (context.Database.GetDbConnection().State != ConnectionState.Open)
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
        }

        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = commandText;

        return (await command.ExecuteScalarAsync(cancellationToken))?.ToString();
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
