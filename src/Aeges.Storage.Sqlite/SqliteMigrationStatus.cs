namespace Aeges.Storage.Sqlite;

/// <summary>
/// Describes EF Core migration status for the local SQLite database.
/// </summary>
/// <param name="DatabasePath">The SQLite database path, when it can be inferred from the connection string.</param>
/// <param name="AppliedMigrations">The migrations already applied to the database.</param>
/// <param name="PendingMigrations">The migrations not yet applied to the database.</param>
public sealed record SqliteMigrationStatus(
    string? DatabasePath,
    IReadOnlyList<string> AppliedMigrations,
    IReadOnlyList<string> PendingMigrations)
{
    /// <summary>
    /// Gets a value indicating whether all known migrations are applied.
    /// </summary>
    public bool IsUpToDate => PendingMigrations.Count == 0;
}
