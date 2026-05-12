using System.Text.Json;

namespace Aeges.Cli.Tests;

public sealed class AegesCliUpdateTests
{
    [Fact]
    public async Task Verbose_flag_writes_trace_to_stderr_without_polluting_stdout()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await AegesCli.RunAsync(
            ["version", "-v"],
            TextReader.Null,
            output,
            error,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.StartsWith("aeges ", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("trace: command: version", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("trace: exit-code: 0", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verbose_flag_keeps_json_stdout_parseable()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await AegesCli.RunAsync(
            ["version", "--json", "--verbose"],
            TextReader.Null,
            output,
            error,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        using var document = JsonDocument.Parse(output.ToString());
        Assert.Equal("aeges", document.RootElement.GetProperty("Name").GetString());
        Assert.Contains("trace: command: version --json", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_dry_run_describes_release_update_plan()
    {
        var output = new StringWriter();

        var exitCode = await AegesCli.RunAsync(
            ["update", "--dry-run", "--version", "0.1.0-alpha.3"],
            TextReader.Null,
            output,
            TextWriter.Null,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.StartsWith("Resolving Aeges update...", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Aeges update dry run.", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Target version: 0.1.0-alpha.3", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Source: (resolved from GitHub Releases)", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_dry_run_supports_local_package_source()
    {
        var packageSource = Path.Combine(Path.GetTempPath(), "aeges-update-source", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(packageSource);
        var output = new StringWriter();

        try
        {
            var exitCode = await AegesCli.RunAsync(
                [
                    "update",
                    "--dry-run",
                    "--version",
                    "0.1.0-alpha.3",
                    "--package-source",
                    packageSource,
                ],
                TextReader.Null,
                output,
                TextWriter.Null,
                CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Contains($"Source: {packageSource}", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(packageSource, recursive: true);
        }
    }

    [Fact]
    public async Task Update_skips_dotnet_tool_update_when_target_matches_current_version()
    {
        var packageSource = Path.Combine(Path.GetTempPath(), "aeges-update-source", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(packageSource);
        var versionOutput = new StringWriter();
        await AegesCli.RunAsync(["version"], TextReader.Null, versionOutput, TextWriter.Null, CancellationToken.None);
        var currentVersion = versionOutput.ToString().Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
        var output = new StringWriter();

        try
        {
            var exitCode = await AegesCli.RunAsync(
                [
                    "update",
                    "--version",
                    currentVersion,
                    "--package-source",
                    packageSource,
                ],
                TextReader.Null,
                output,
                TextWriter.Null,
                CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Contains("Aeges is already up to date.", output.ToString(), StringComparison.Ordinal);
            Assert.Contains($"Target version: {currentVersion}", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(packageSource, recursive: true);
        }
    }

    [Fact]
    public async Task Update_dry_run_supports_json_output()
    {
        var output = new StringWriter();

        var exitCode = await AegesCli.RunAsync(
            ["update", "--dry-run", "--json", "--version", "0.1.0-alpha.3"],
            TextReader.Null,
            output,
            TextWriter.Null,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        using var document = JsonDocument.Parse(output.ToString());
        Assert.True(document.RootElement.GetProperty("DryRun").GetBoolean());
        Assert.Equal("0.1.0-alpha.3", document.RootElement.GetProperty("TargetVersion").GetString());
        Assert.Equal("Aeges.Cli", document.RootElement.GetProperty("ToolPackage").GetString());
    }

    [Fact]
    public async Task Update_accepts_windows_self_update_options()
    {
        var output = new StringWriter();

        var exitCode = await AegesCli.RunAsync(
            ["update", "--dry-run", "--direct", "--elevated", "--version", "0.1.0-alpha.3"],
            TextReader.Null,
            output,
            TextWriter.Null,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("Aeges update dry run.", output.ToString(), StringComparison.Ordinal);
    }
}
