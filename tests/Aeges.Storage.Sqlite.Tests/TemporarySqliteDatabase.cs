using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite.Tests;

internal sealed class TemporarySqliteDatabase : IAsyncDisposable
{
    private TemporarySqliteDatabase(string databasePath)
    {
        DatabasePath = databasePath;
    }

    public string DatabasePath { get; }

    public static async Task<TemporarySqliteDatabase> CreateAsync()
    {
        var database = new TemporarySqliteDatabase(
            Path.Combine(Path.GetTempPath(), $"aeges-{Guid.NewGuid():N}.db"));

        await using var context = database.CreateContext();
        await SqlitePragmas.ApplyAsync(context, CancellationToken.None);
        await context.Database.MigrateAsync(CancellationToken.None);

        return database;
    }

    public AegesDbContext CreateContext() =>
        new(AegesDbContextOptions.Create($"Data Source={DatabasePath}"));

    public ValueTask DisposeAsync()
    {
        DeleteIfExists(DatabasePath);
        DeleteIfExists($"{DatabasePath}-shm");
        DeleteIfExists($"{DatabasePath}-wal");

        return ValueTask.CompletedTask;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
