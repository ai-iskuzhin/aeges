using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Aeges.Storage.Sqlite;

/// <summary>
/// Creates <see cref="AegesDbContext"/> instances for EF Core design-time commands.
/// </summary>
public sealed class AegesDbContextFactory : IDesignTimeDbContextFactory<AegesDbContext>
{
    /// <inheritdoc />
    public AegesDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString(args);
        var options = new DbContextOptionsBuilder<AegesDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new AegesDbContext(options);
    }

    private static string ResolveConnectionString(IEnumerable<string> args)
    {
        var argsList = args.ToArray();
        var connectionStringIndex = Array.IndexOf(argsList, "--connection-string");

        if (connectionStringIndex >= 0 && connectionStringIndex + 1 < argsList.Length)
        {
            return argsList[connectionStringIndex + 1];
        }

        return Environment.GetEnvironmentVariable("AEGES_SQLITE_CONNECTION_STRING")
            ?? "Data Source=aeges.db";
    }
}
