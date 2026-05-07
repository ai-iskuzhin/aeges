using Aeges.Storage.Sqlite;
using Microsoft.EntityFrameworkCore;

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
        };

        Assert.True(expectedTableNames.SetEquals(tableNames));
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
