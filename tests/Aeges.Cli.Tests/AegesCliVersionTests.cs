using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aeges.Cli.Tests;

public sealed class AegesCliVersionTests
{
    [Fact]
    public async Task Version_prints_cli_version()
    {
        var output = new StringWriter();

        var exitCode = await AegesCli.RunAsync(
            ["version"],
            TextReader.Null,
            output,
            TextWriter.Null,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Matches(
            new Regex(@"^aeges \d+\.\d+\.\d+", RegexOptions.CultureInvariant),
            output.ToString().Trim());
    }

    [Fact]
    public async Task Version_supports_json_output()
    {
        var output = new StringWriter();

        var exitCode = await AegesCli.RunAsync(
            ["version", "--json"],
            TextReader.Null,
            output,
            TextWriter.Null,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        using var document = JsonDocument.Parse(output.ToString());
        Assert.Equal("aeges", document.RootElement.GetProperty("Name").GetString());
        Assert.Matches(
            new Regex(@"^\d+\.\d+\.\d+", RegexOptions.CultureInvariant),
            document.RootElement.GetProperty("Version").GetString()!);
    }
}
