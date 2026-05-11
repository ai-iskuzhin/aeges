using System.Text.Json;

namespace Aeges.Cli.Tests;

public sealed class AegesCliInitTests
{
    [Fact]
    public async Task Init_migrates_database_and_registers_project_and_machine()
    {
        using var runtime = new TemporaryRuntime();
        var output = new StringWriter();

        var exitCode = await AegesCli.RunAsync(
            [
                "init",
                "--project-id",
                "project-init",
                "--project-name",
                "Init Project",
                "--path",
                runtime.ProjectPath,
                "--machine-id",
                "machine-init",
                "--machine-name",
                "Init Machine",
                "--platform",
                "test",
                "--connection-string",
                runtime.ConnectionString,
            ],
            TextReader.Null,
            output,
            TextWriter.Null,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("Aeges initialized.", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Project: project-init (created)", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Machine: machine-init (created)", output.ToString(), StringComparison.Ordinal);

        var status = new StringWriter();
        var statusExitCode = await AegesCli.RunAsync(
            ["status", "--connection-string", runtime.ConnectionString],
            TextReader.Null,
            status,
            TextWriter.Null,
            CancellationToken.None);

        Assert.Equal(0, statusExitCode);
        Assert.Contains("Projects: 1", status.ToString(), StringComparison.Ordinal);
        Assert.Contains("Machines: 1", status.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Init_is_idempotent_for_existing_project_and_machine()
    {
        using var runtime = new TemporaryRuntime();
        var args = new[]
        {
            "init",
            "--json",
            "--project-id",
            "project-init",
            "--path",
            runtime.ProjectPath,
            "--machine-id",
            "machine-init",
            "--connection-string",
            runtime.ConnectionString,
        };

        var first = new StringWriter();
        var second = new StringWriter();

        Assert.Equal(0, await AegesCli.RunAsync(args, TextReader.Null, first, TextWriter.Null, CancellationToken.None));
        Assert.Equal(0, await AegesCli.RunAsync(args, TextReader.Null, second, TextWriter.Null, CancellationToken.None));

        using var firstDocument = JsonDocument.Parse(first.ToString());
        using var secondDocument = JsonDocument.Parse(second.ToString());

        Assert.True(firstDocument.RootElement.GetProperty("ProjectCreated").GetBoolean());
        Assert.True(firstDocument.RootElement.GetProperty("MachineCreated").GetBoolean());
        Assert.False(secondDocument.RootElement.GetProperty("ProjectCreated").GetBoolean());
        Assert.False(secondDocument.RootElement.GetProperty("MachineCreated").GetBoolean());
    }

    private sealed class TemporaryRuntime : IDisposable
    {
        public TemporaryRuntime()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "aeges-cli-init", Guid.NewGuid().ToString("N"));
            ProjectPath = Path.Combine(RootPath, "project");
            Directory.CreateDirectory(ProjectPath);
            DatabasePath = Path.Combine(RootPath, "aeges.db");
            ConnectionString = $"Data Source={DatabasePath}";
        }

        public string RootPath { get; }

        public string ProjectPath { get; }

        public string DatabasePath { get; }

        public string ConnectionString { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
