using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite;

/// <summary>
/// Provides SQLite migration status and migration execution operations.
/// </summary>
public sealed class SqliteMigrationService
{
    private readonly string connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteMigrationService"/> class.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    public SqliteMigrationService(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string must not be empty.", nameof(connectionString));
        }

        this.connectionString = connectionString;
    }

    /// <summary>
    /// Gets migration status for the SQLite database.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The migration status.</returns>
    public async Task<SqliteMigrationStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        await using var context = CreateContext();
        await SqlitePragmas.ApplyAsync(context, cancellationToken);

        var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
        var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken);

        return new SqliteMigrationStatus(
            InferDatabasePath(),
            applied.ToArray(),
            pending.ToArray());
    }

    /// <summary>
    /// Applies pending migrations and returns the resulting status.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The migration status after migrations are applied.</returns>
    public async Task<SqliteMigrationStatus> MigrateAsync(CancellationToken cancellationToken)
    {
        await using var context = CreateContext();
        await SqlitePragmas.ApplyAsync(context, cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);

        return await GetStatusAsync(cancellationToken);
    }

    private AegesDbContext CreateContext() =>
        new(AegesDbContextOptions.Create(connectionString));

    private string? InferDatabasePath()
    {
        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);

        return string.IsNullOrWhiteSpace(builder.DataSource)
            ? null
            : builder.DataSource;
    }
}
