using System.Text.Json;

namespace Aeges.Cli.Tests;

public sealed class AegesCliStatusTests
{
    [Fact]
    public async Task Status_reports_runtime_counts_for_ready_database()
    {
        using var database = new TemporaryDatabase();

        await RunAsync(["db", "migrate", "--connection-string", database.ConnectionString]);
        await RunAsync([
            "project",
            "add",
            "--project-id",
            "project-test",
            "--name",
            "Test",
            "--path",
            database.DirectoryPath,
            "--connection-string",
            database.ConnectionString,
        ]);
        await RunAsync([
            "machine",
            "add",
            "--machine-id",
            "machine-test",
            "--name",
            "Machine",
            "--platform",
            "test",
            "--connection-string",
            database.ConnectionString,
        ]);
        await RunAsync([
            "task",
            "create",
            "--task-id",
            "task-test",
            "--project-id",
            "project-test",
            "--machine-id",
            "machine-test",
            "--title",
            "Task",
            "--goal",
            "Check status output.",
            "--connection-string",
            database.ConnectionString,
        ]);

        var output = new StringWriter();
        var exitCode = await AegesCli.RunAsync(
            ["status", "--connection-string", database.ConnectionString],
            TextReader.Null,
            output,
            TextWriter.Null,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("Aeges local status", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Database status: up-to-date", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Projects: 1", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Machines: 1", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("  - queued: 1", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Status_json_reports_pending_database_without_creating_schema()
    {
        using var database = new TemporaryDatabase();
        var output = new StringWriter();

        var exitCode = await AegesCli.RunAsync(
            ["status", "--json", "--connection-string", database.ConnectionString],
            TextReader.Null,
            output,
            TextWriter.Null,
            CancellationToken.None);

        Assert.Equal(1, exitCode);

        using var document = JsonDocument.Parse(output.ToString());
        Assert.False(document.RootElement.GetProperty("Database").GetProperty("IsUpToDate").GetBoolean());
        Assert.True(document.RootElement.TryGetProperty("TaskCounts", out var taskCounts));
        Assert.Equal(JsonValueKind.Array, taskCounts.ValueKind);
        Assert.Equal(0, taskCounts.GetArrayLength());
    }

    private static async Task RunAsync(string[] args)
    {
        var exitCode = await AegesCli.RunAsync(
            args,
            TextReader.Null,
            TextWriter.Null,
            TextWriter.Null,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
    }

    private sealed class TemporaryDatabase : IDisposable
    {
        public TemporaryDatabase()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "aeges-cli-status", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            DatabasePath = Path.Combine(DirectoryPath, "aeges.db");
            ConnectionString = $"Data Source={DatabasePath}";
        }

        public string DirectoryPath { get; }

        public string DatabasePath { get; }

        public string ConnectionString { get; }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
