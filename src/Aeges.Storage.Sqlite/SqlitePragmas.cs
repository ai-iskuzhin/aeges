using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite;

/// <summary>
/// Applies SQLite pragmas required for the local Aeges runtime database.
/// </summary>
public static class SqlitePragmas
{
    /// <summary>
    /// Applies WAL, foreign key, and busy timeout pragmas.
    /// </summary>
    /// <param name="context">The database context whose connection should be configured.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task ApplyAsync(AegesDbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        await context.Database.OpenConnectionAsync(cancellationToken);

        await ExecuteScalarAsync(context, "PRAGMA journal_mode = WAL;", cancellationToken);
        await ExecuteScalarAsync(context, "PRAGMA foreign_keys = ON;", cancellationToken);
        await ExecuteScalarAsync(context, "PRAGMA busy_timeout = 5000;", cancellationToken);
    }

    private static async Task<object?> ExecuteScalarAsync(
        AegesDbContext context,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = commandText;

        return await command.ExecuteScalarAsync(cancellationToken);
    }
}
