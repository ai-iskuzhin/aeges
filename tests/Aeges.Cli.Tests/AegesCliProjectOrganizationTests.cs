using System.Text.Json;

namespace Aeges.Cli.Tests;

public sealed class AegesCliProjectOrganizationTests
{
    [Fact]
    public async Task Root_scan_registers_projects_and_assigns_matching_groups()
    {
        using var runtime = new TemporaryRuntime();
        var groupPath = Path.Combine(runtime.WorkPath, "analitex");
        var groupedProjectPath = Path.Combine(groupPath, "api");
        var ungroupedProjectPath = Path.Combine(runtime.WorkPath, "scratch");
        Directory.CreateDirectory(groupedProjectPath);
        Directory.CreateDirectory(ungroupedProjectPath);
        File.WriteAllText(Path.Combine(groupedProjectPath, "package.json"), "{}");
        File.WriteAllText(Path.Combine(ungroupedProjectPath, "pyproject.toml"), "[project]\nname = \"scratch\"\n");

        Assert.Equal(
            0,
            await AegesCli.RunAsync(
                [
                    "group",
                    "add",
                    "--group-id",
                    "analitex",
                    "--name",
                    "Analitex",
                    "--path",
                    groupPath,
                    "--connection-string",
                    runtime.ConnectionString,
                ],
                TextReader.Null,
                TextWriter.Null,
                TextWriter.Null,
                CancellationToken.None));
        Assert.Equal(
            0,
            await AegesCli.RunAsync(
                [
                    "root",
                    "add",
                    "--root-id",
                    "work",
                    "--name",
                    "Work",
                    "--path",
                    runtime.WorkPath,
                    "--connection-string",
                    runtime.ConnectionString,
                ],
                TextReader.Null,
                TextWriter.Null,
                TextWriter.Null,
                CancellationToken.None));

        var scanOutput = new StringWriter();
        var scanExitCode = await AegesCli.RunAsync(
            [
                "root",
                "scan",
                "work",
                "--max-depth",
                "2",
                "--apply",
                "--json",
                "--connection-string",
                runtime.ConnectionString,
            ],
            TextReader.Null,
            scanOutput,
            TextWriter.Null,
            CancellationToken.None);

        Assert.Equal(0, scanExitCode);

        using var scanDocument = JsonDocument.Parse(scanOutput.ToString());
        Assert.Equal(2, scanDocument.RootElement.GetProperty("Candidates").GetArrayLength());
        Assert.Contains(
            scanDocument.RootElement.GetProperty("Candidates").EnumerateArray(),
            candidate => candidate.GetProperty("Name").GetString() == "api"
                && candidate.GetProperty("GroupId").GetString() == "analitex"
                && candidate.GetProperty("Status").GetString() == "created");
        Assert.Contains(
            scanDocument.RootElement.GetProperty("Candidates").EnumerateArray(),
            candidate => candidate.GetProperty("Name").GetString() == "scratch"
                && candidate.GetProperty("GroupId").ValueKind == JsonValueKind.Null);

        var projectListOutput = new StringWriter();
        var projectListExitCode = await AegesCli.RunAsync(
            ["project", "list", "--json", "--connection-string", runtime.ConnectionString],
            TextReader.Null,
            projectListOutput,
            TextWriter.Null,
            CancellationToken.None);

        Assert.Equal(0, projectListExitCode);

        using var projectListDocument = JsonDocument.Parse(projectListOutput.ToString());
        Assert.Equal(2, projectListDocument.RootElement.GetArrayLength());
        Assert.Contains(
            projectListDocument.RootElement.EnumerateArray(),
            project => project.GetProperty("Name").GetString() == "api"
                && project.GetProperty("GroupId").GetString() == "analitex");
        Assert.Contains(
            projectListDocument.RootElement.EnumerateArray(),
            project => project.GetProperty("Name").GetString() == "scratch"
                && project.GetProperty("GroupId").ValueKind == JsonValueKind.Null);
    }

    private sealed class TemporaryRuntime : IDisposable
    {
        public TemporaryRuntime()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "aeges-cli-projects", Guid.NewGuid().ToString("N"));
            WorkPath = Path.Combine(RootPath, "work");
            Directory.CreateDirectory(WorkPath);
            DatabasePath = Path.Combine(RootPath, "aeges.db");
            ConnectionString = $"Data Source={DatabasePath}";
        }

        public string RootPath { get; }

        public string WorkPath { get; }

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
