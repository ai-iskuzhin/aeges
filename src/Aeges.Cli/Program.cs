using System.Text.Json;
using Aeges.Application.Configuration;
using Aeges.Application.Runtime;
using Aeges.Storage.Sqlite;

return await AegesCli.RunAsync(args, Console.Out, Console.Error, CancellationToken.None);

internal static class AegesCli
{
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args is ["db", "status", .. var statusArgs])
        {
            return await RunDatabaseStatusAsync(statusArgs, output, error, cancellationToken);
        }

        if (args is ["db", "migrate", .. var migrateArgs])
        {
            return await RunDatabaseMigrateAsync(migrateArgs, output, error, cancellationToken);
        }

        await WriteUsageAsync(error);

        return 2;
    }

    private static async Task<int> RunDatabaseStatusAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        var service = CreateMigrationService(options);
        var status = await service.GetStatusAsync(cancellationToken);
        await WriteStatusAsync(status, options.Json, output);

        return 0;
    }

    private static async Task<int> RunDatabaseMigrateAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        var service = CreateMigrationService(options);
        var status = await service.MigrateAsync(cancellationToken);
        await WriteStatusAsync(status, options.Json, output);

        return 0;
    }

    private static SqliteMigrationService CreateMigrationService(CliOptions options)
    {
        if (options.ConnectionString is not null)
        {
            return new SqliteMigrationService(options.ConnectionString);
        }

        var layout = RuntimeDirectoryLayout.CreateDefault();
        var configuration = new AegesConfigurationLoader().Load(
            new AegesConfigurationLoaderOptions(options.ConfigPath));

        var connectionString = configuration.Storage.ConnectionString;

        if (connectionString is null)
        {
            foreach (var directory in layout.RequiredDirectories)
            {
                Directory.CreateDirectory(directory);
            }

            connectionString = $"Data Source={layout.DatabasePath}";
        }

        return new SqliteMigrationService(connectionString);
    }

    private static async Task WriteStatusAsync(
        SqliteMigrationStatus status,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                status,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"Database: {status.DatabasePath ?? "(unknown)"}");
        await output.WriteLineAsync($"Status: {(status.IsUpToDate ? "up-to-date" : "pending migrations")}");
        await output.WriteLineAsync($"Applied migrations: {status.AppliedMigrations.Count}");
        foreach (var migration in status.AppliedMigrations)
        {
            await output.WriteLineAsync($"  - {migration}");
        }

        await output.WriteLineAsync($"Pending migrations: {status.PendingMigrations.Count}");
        foreach (var migration in status.PendingMigrations)
        {
            await output.WriteLineAsync($"  - {migration}");
        }
    }

    private static async Task WriteUsageAsync(TextWriter error)
    {
        await error.WriteLineAsync("Usage:");
        await error.WriteLineAsync("  aeges db status [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges db migrate [--config <path>] [--connection-string <value>] [--json]");
    }

    private sealed class CliOptions
    {
        public string? ConfigPath { get; private init; }

        public string? ConnectionString { get; private init; }

        public bool Json { get; private init; }

        public string? Error { get; private init; }

        public static CliOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            var json = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--json":
                        json = true;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return new CliOptions { Error = "--config requires a value." };
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return new CliOptions { Error = "--connection-string requires a value." };
                        }

                        break;
                    default:
                        return new CliOptions { Error = $"Unknown option '{args[index]}'." };
                }
            }

            return new CliOptions
            {
                ConfigPath = configPath,
                ConnectionString = connectionString,
                Json = json,
            };
        }

        private static bool TryReadValue(string[] args, ref int index, out string? value)
        {
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = null;
                return false;
            }

            index++;
            value = args[index];

            return true;
        }
    }
}
