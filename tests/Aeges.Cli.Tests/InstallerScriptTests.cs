using System.Text.RegularExpressions;

namespace Aeges.Cli.Tests;

public sealed class InstallerScriptTests
{
    [Fact]
    public async Task PowerShell_installer_avoids_ambiguous_variable_colon_interpolation()
    {
        var script = await File.ReadAllTextAsync(
            Path.Combine(FindRepositoryRoot(), "scripts", "install.ps1"),
            CancellationToken.None);
        var matches = Regex.Matches(
            script,
            "\\$(?!env:|script:)[A-Za-z_][A-Za-z0-9_]*:",
            RegexOptions.CultureInvariant);

        Assert.Empty(matches.Select(match => match.Value));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Aeges.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the Aeges repository root.");
    }
}
