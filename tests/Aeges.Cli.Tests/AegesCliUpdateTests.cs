using System.Text.Json;

namespace Aeges.Cli.Tests;

public sealed class AegesCliUpdateTests
{
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
}
