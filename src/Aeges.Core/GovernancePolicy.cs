using System.Text.RegularExpressions;

namespace Aeges.Core;

/// <summary>
/// Defines deterministic governance limits used before runner dispatch.
/// </summary>
public sealed class GovernancePolicy
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GovernancePolicy"/> class.
    /// </summary>
    /// <param name="maxIterations">The maximum number of task iterations allowed.</param>
    /// <param name="timeout">The maximum runner execution timeout allowed.</param>
    /// <param name="allowedPathPatterns">Path patterns execution may modify. Empty means all relative paths are allowed.</param>
    /// <param name="deniedPathPatterns">Path patterns execution must not modify.</param>
    /// <param name="approvalRequiredPathPatterns">Path patterns that require an approval checkpoint before modification.</param>
    /// <param name="approvalRequiredCommandPatterns">Command fragments that require an approval checkpoint before execution.</param>
    public GovernancePolicy(
        int maxIterations,
        TimeSpan timeout,
        IReadOnlyList<string>? allowedPathPatterns = null,
        IReadOnlyList<string>? deniedPathPatterns = null,
        IReadOnlyList<string>? approvalRequiredPathPatterns = null,
        IReadOnlyList<string>? approvalRequiredCommandPatterns = null)
    {
        if (maxIterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxIterations), maxIterations, "Max iterations must be greater than zero.");
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be greater than zero.");
        }

        MaxIterations = maxIterations;
        Timeout = timeout;
        AllowedPathPatterns = CopyPathPatterns(allowedPathPatterns);
        DeniedPathPatterns = CopyPathPatterns(deniedPathPatterns);
        ApprovalRequiredPathPatterns = CopyPathPatterns(approvalRequiredPathPatterns);
        ApprovalRequiredCommandPatterns = CopyCommandPatterns(approvalRequiredCommandPatterns);
    }

    /// <summary>
    /// Gets the maximum number of task iterations allowed.
    /// </summary>
    public int MaxIterations { get; }

    /// <summary>
    /// Gets the maximum runner execution timeout allowed.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Gets path patterns execution may modify. Empty means all relative paths are allowed.
    /// </summary>
    public IReadOnlyList<string> AllowedPathPatterns { get; }

    /// <summary>
    /// Gets path patterns execution must not modify.
    /// </summary>
    public IReadOnlyList<string> DeniedPathPatterns { get; }

    /// <summary>
    /// Gets path patterns that require an approval checkpoint before modification.
    /// </summary>
    public IReadOnlyList<string> ApprovalRequiredPathPatterns { get; }

    /// <summary>
    /// Gets command fragments that require an approval checkpoint before execution.
    /// </summary>
    public IReadOnlyList<string> ApprovalRequiredCommandPatterns { get; }

    /// <summary>
    /// Creates the default local MVP governance policy.
    /// </summary>
    /// <returns>The default governance policy.</returns>
    public static GovernancePolicy CreateDefault() =>
        new(
            maxIterations: RuntimeTask.DefaultMaxIterations,
            timeout: TimeSpan.FromMinutes(30),
            approvalRequiredPathPatterns:
            [
                "package.json",
                "package-lock.json",
                "pnpm-lock.yaml",
                "yarn.lock",
                "*.csproj",
                "**/*.csproj",
                "Directory.Packages.props",
                "**/Migrations/*",
                ".github/workflows/*",
                "scripts/deploy*",
            ],
            approvalRequiredCommandPatterns:
            [
                "git reset --hard",
                "git clean",
                "git push",
                "git branch -D",
                "git branch -d",
                "git rebase",
            ]);

    /// <summary>
    /// Determines whether a relative path matches any configured allowed path pattern.
    /// </summary>
    /// <param name="relativePath">The relative path to check.</param>
    /// <returns><see langword="true"/> when the path is allowed; otherwise <see langword="false"/>.</returns>
    public bool IsPathAllowed(string relativePath)
    {
        var normalizedPath = GovernancePathPattern.NormalizePath(relativePath);

        return AllowedPathPatterns.Count == 0
            || AllowedPathPatterns.Any(pattern => MatchesPathPattern(pattern, normalizedPath));
    }

    /// <summary>
    /// Determines whether a relative path matches a configured denied path pattern.
    /// </summary>
    /// <param name="relativePath">The relative path to check.</param>
    /// <returns><see langword="true"/> when the path is denied; otherwise <see langword="false"/>.</returns>
    public bool IsPathDenied(string relativePath)
    {
        var normalizedPath = GovernancePathPattern.NormalizePath(relativePath);

        return DeniedPathPatterns.Any(pattern => MatchesPathPattern(pattern, normalizedPath));
    }

    /// <summary>
    /// Determines whether a relative path requires approval before modification.
    /// </summary>
    /// <param name="relativePath">The relative path to check.</param>
    /// <returns><see langword="true"/> when approval is required; otherwise <see langword="false"/>.</returns>
    public bool DoesPathRequireApproval(string relativePath)
    {
        var normalizedPath = GovernancePathPattern.NormalizePath(relativePath);

        return ApprovalRequiredPathPatterns.Any(pattern => MatchesPathPattern(pattern, normalizedPath));
    }

    /// <summary>
    /// Determines whether a command requires approval before execution.
    /// </summary>
    /// <param name="command">The command to check.</param>
    /// <returns><see langword="true"/> when approval is required; otherwise <see langword="false"/>.</returns>
    public bool DoesCommandRequireApproval(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Command must not be empty.", nameof(command));
        }

        return ApprovalRequiredCommandPatterns.Any(
            pattern => command.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> CopyPathPatterns(IReadOnlyList<string>? patterns) =>
        patterns is null
            ? []
            : patterns.Select(GovernancePathPattern.Require).ToArray();

    private static IReadOnlyList<string> CopyCommandPatterns(IReadOnlyList<string>? patterns)
    {
        if (patterns is null)
        {
            return [];
        }

        if (patterns.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Command patterns must not be empty.", nameof(patterns));
        }

        return patterns.ToArray();
    }

    private static bool MatchesPathPattern(string pattern, string normalizedPath)
    {
        var normalizedPattern = GovernancePathPattern.NormalizePath(pattern);
        var regexPattern = "^" + Regex.Escape(normalizedPattern)
            .Replace("\\*\\*", ".*", StringComparison.Ordinal)
            .Replace("\\*", "[^/]*", StringComparison.Ordinal)
            + "$";

        return Regex.IsMatch(normalizedPath, regexPattern, RegexOptions.CultureInvariant);
    }
}

internal static class GovernancePathPattern
{
    public static string Require(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Path pattern must not be empty.", nameof(value));
        }

        return NormalizePath(value);
    }

    public static string NormalizePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Path must not be empty.", nameof(value));
        }

        if (Path.IsPathRooted(value) || HasWindowsRoot(value))
        {
            throw new ArgumentException("Path must be relative.", nameof(value));
        }

        var normalized = value.Replace('\\', '/');

        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("Path must not contain current or parent directory segments.", nameof(value));
        }

        return string.Join("/", segments);
    }

    private static bool HasWindowsRoot(string value) =>
        value.StartsWith(@"\\", StringComparison.Ordinal)
        || (value.Length >= 3 && char.IsAsciiLetter(value[0]) && value[1] == ':' && value[2] is '\\' or '/');
}
