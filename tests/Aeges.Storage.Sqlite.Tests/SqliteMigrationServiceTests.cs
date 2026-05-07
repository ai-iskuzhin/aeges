using Aeges.Storage.Sqlite;

namespace Aeges.Storage.Sqlite.Tests;

public sealed class SqliteMigrationServiceTests
{
    [Fact]
    public async Task GetStatusAsync_reports_pending_migrations_for_empty_database()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var service = new SqliteMigrationService($"Data Source={databasePath}");

            var status = await service.GetStatusAsync(CancellationToken.None);

            Assert.Equal(databasePath, status.DatabasePath);
            Assert.Empty(status.AppliedMigrations);
            Assert.Contains("20260507172913_InitialCreate", status.PendingMigrations);
            Assert.False(status.IsUpToDate);
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    [Fact]
    public async Task MigrateAsync_applies_pending_migrations()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var service = new SqliteMigrationService($"Data Source={databasePath}");

            var status = await service.MigrateAsync(CancellationToken.None);

            Assert.Contains("20260507172913_InitialCreate", status.AppliedMigrations);
            Assert.Empty(status.PendingMigrations);
            Assert.True(status.IsUpToDate);
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    [Fact]
    public void Create_rejects_empty_connection_string()
    {
        Assert.Throws<ArgumentException>(() => new SqliteMigrationService(" "));
    }

    private static string CreateDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"aeges-migrations-{Guid.NewGuid():N}.db");

    private static void DeleteDatabaseFiles(string databasePath)
    {
        DeleteIfExists(databasePath);
        DeleteIfExists($"{databasePath}-shm");
        DeleteIfExists($"{databasePath}-wal");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
