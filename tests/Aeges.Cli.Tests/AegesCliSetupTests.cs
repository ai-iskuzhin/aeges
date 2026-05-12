using System.Text.Json;

namespace Aeges.Cli.Tests;

public sealed class AegesCliSetupTests
{
    [Fact]
    public async Task Setup_migrates_database_and_registers_local_runtime()
    {
        using var runtime = new TemporaryRuntime();
        var output = new StringWriter();

        var exitCode = await AegesCli.RunAsync(
            [
                "setup",
                "--skip-telegram",
                "--no-start",
                "--project-id",
                "project-setup",
                "--project-name",
                "Setup Project",
                "--path",
                runtime.ProjectPath,
                "--machine-id",
                "machine-setup",
                "--machine-name",
                "Setup Machine",
                "--platform",
                "test",
                "--connection-string",
                runtime.ConnectionString,
            ],
            new StringReader(""),
            output,
            TextWriter.Null,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("Aeges setup", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Aeges initialized.", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Project: project-setup (created)", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Machine: machine-setup (created)", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Setup complete.", output.ToString(), StringComparison.Ordinal);

        var status = new StringWriter();
        var statusExitCode = await AegesCli.RunAsync(
            ["status", "--connection-string", runtime.ConnectionString],
            TextReader.Null,
            status,
            TextWriter.Null,
            CancellationToken.None);

        Assert.Equal(0, statusExitCode);
        Assert.Contains("Database status: up-to-date", status.ToString(), StringComparison.Ordinal);
        Assert.Contains("Projects: 1", status.ToString(), StringComparison.Ordinal);
        Assert.Contains("Machines: 1", status.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Setup_rejects_json_because_it_is_interactive()
    {
        var error = new StringWriter();

        var exitCode = await AegesCli.RunAsync(
            ["setup", "--json"],
            TextReader.Null,
            TextWriter.Null,
            error,
            CancellationToken.None);

        Assert.Equal(2, exitCode);
        Assert.Contains("setup is interactive", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Setup_can_skip_telegram_from_the_wizard_prompt()
    {
        using var runtime = new TemporaryRuntime();
        var output = new StringWriter();

        var exitCode = await AegesCli.RunAsync(
            [
                "setup",
                "--no-start",
                "--project-id",
                "project-no-telegram",
                "--path",
                runtime.ProjectPath,
                "--machine-id",
                "machine-no-telegram",
                "--connection-string",
                runtime.ConnectionString,
                "--config",
                runtime.ConfigPath,
            ],
            new StringReader("n\n"),
            output,
            TextWriter.Null,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("Set up Telegram now [Y/n]:", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Setup complete.", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Init_remains_the_scriptable_json_setup_path()
    {
        using var runtime = new TemporaryRuntime();
        var output = new StringWriter();

        var exitCode = await AegesCli.RunAsync(
            [
                "init",
                "--json",
                "--project-id",
                "project-setup-json",
                "--path",
                runtime.ProjectPath,
                "--machine-id",
                "machine-setup-json",
                "--connection-string",
                runtime.ConnectionString,
            ],
            TextReader.Null,
            output,
            TextWriter.Null,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        using var document = JsonDocument.Parse(output.ToString());
        Assert.True(document.RootElement.GetProperty("DatabaseUpToDate").GetBoolean());
        Assert.True(document.RootElement.GetProperty("ProjectCreated").GetBoolean());
        Assert.True(document.RootElement.GetProperty("MachineCreated").GetBoolean());
    }

    private sealed class TemporaryRuntime : IDisposable
    {
        public TemporaryRuntime()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "aeges-cli-setup", Guid.NewGuid().ToString("N"));
            ProjectPath = Path.Combine(RootPath, "project");
            Directory.CreateDirectory(ProjectPath);
            DatabasePath = Path.Combine(RootPath, "aeges.db");
            ConfigPath = Path.Combine(RootPath, "config.json");
            ConnectionString = $"Data Source={DatabasePath}";
            File.WriteAllText(
                ConfigPath,
                $$"""
                {
                  "machineId": "local",
                  "telegram": {
                    "botTokenEnvironmentVariable": "AEGES_TEST_TELEGRAM_BOT_TOKEN_{{Guid.NewGuid():N}}"
                  }
                }
                """);
        }

        public string RootPath { get; }

        public string ProjectPath { get; }

        public string DatabasePath { get; }

        public string ConfigPath { get; }

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
