using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite;

/// <summary>
/// Creates EF Core options for the Aeges SQLite context.
/// </summary>
public static class AegesDbContextOptions
{
    /// <summary>
    /// Creates context options for a SQLite connection string.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <returns>Configured context options.</returns>
    public static DbContextOptions<AegesDbContext> Create(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string must not be empty.", nameof(connectionString));
        }

        return new DbContextOptionsBuilder<AegesDbContext>()
            .UseSqlite(connectionString)
            .Options;
    }
}
