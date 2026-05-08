namespace Aeges.Runners.Codex;

/// <summary>
/// Resolves Codex CLI executables from absolute paths or the process PATH.
/// </summary>
public sealed class PathCodexExecutableResolver : ICodexExecutableResolver
{
    /// <inheritdoc />
    public string? Resolve(string executable)
    {
        if (string.IsNullOrWhiteSpace(executable))
        {
            throw new ArgumentException("Executable must not be empty.", nameof(executable));
        }

        if (ContainsDirectorySegment(executable))
        {
            return File.Exists(executable)
                ? Path.GetFullPath(executable)
                : null;
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var executableName in CandidateExecutableNames(executable))
            {
                var candidate = Path.Combine(directory, executableName);

                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
        }

        return null;
    }

    private static bool ContainsDirectorySegment(string executable) =>
        Path.IsPathFullyQualified(executable)
        || executable.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
        || executable.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal);

    private static IEnumerable<string> CandidateExecutableNames(string executable)
    {
        yield return executable;

        if (!OperatingSystem.IsWindows() || Path.HasExtension(executable))
        {
            yield break;
        }

        var pathExtensions = Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM";

        foreach (var extension in pathExtensions.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            yield return executable + extension;
        }
    }
}
