using Aeges.Core;

namespace Aeges.Git;

/// <summary>
/// Builds deterministic, safe worktree paths under the configured worktree root.
/// </summary>
public static class GitWorktreePathBuilder
{
    /// <summary>
    /// Builds a task-scoped worktree path.
    /// </summary>
    /// <param name="worktreeRootPath">The root directory where worktrees are stored.</param>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="taskId">The task identifier.</param>
    /// <returns>The absolute worktree path.</returns>
    public static string BuildTaskPath(
        string worktreeRootPath,
        ProjectId projectId,
        TaskId taskId) =>
        BuildPath(worktreeRootPath, projectId.Value, taskId.Value);

    /// <summary>
    /// Builds an iteration-scoped worktree path.
    /// </summary>
    /// <param name="worktreeRootPath">The root directory where worktrees are stored.</param>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="iterationId">The iteration identifier.</param>
    /// <returns>The absolute worktree path.</returns>
    public static string BuildIterationPath(
        string worktreeRootPath,
        ProjectId projectId,
        TaskId taskId,
        IterationId iterationId) =>
        BuildPath(worktreeRootPath, projectId.Value, taskId.Value, iterationId.Value);

    /// <summary>
    /// Ensures a path is contained within a configured worktree root.
    /// </summary>
    /// <param name="worktreeRootPath">The root directory where worktrees are stored.</param>
    /// <param name="candidatePath">The path to validate.</param>
    /// <returns>The normalized absolute candidate path.</returns>
    public static string RequireInsideRoot(string worktreeRootPath, string candidatePath)
    {
        var root = RequireAbsoluteRoot(worktreeRootPath);
        var candidate = Path.GetFullPath(RequireText(candidatePath, nameof(candidatePath)));

        if (!IsSubpath(root, candidate))
        {
            throw new ArgumentException("Worktree path must be inside the configured worktree root.", nameof(candidatePath));
        }

        return candidate;
    }

    private static string BuildPath(string worktreeRootPath, params string[] segments)
    {
        var root = RequireAbsoluteRoot(worktreeRootPath);
        var safeSegments = segments.Select(SafeSegment).ToArray();
        var candidate = Path.GetFullPath(Path.Combine([root, .. safeSegments]));

        if (!IsSubpath(root, candidate))
        {
            throw new ArgumentException("Generated worktree path escaped the configured worktree root.", nameof(worktreeRootPath));
        }

        return candidate;
    }

    private static string RequireAbsoluteRoot(string worktreeRootPath)
    {
        var requestedRoot = RequireText(worktreeRootPath, nameof(worktreeRootPath));

        if (!Path.IsPathFullyQualified(requestedRoot))
        {
            throw new ArgumentException("Worktree root path must be absolute.", nameof(worktreeRootPath));
        }

        var root = Path.GetFullPath(requestedRoot);

        return root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string SafeSegment(string value)
    {
        var segment = RequireText(value, nameof(value));
        var safe = string.Concat(segment.Select(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'
                ? character
                : '_'));

        if (safe is "." or ".." || string.IsNullOrWhiteSpace(safe))
        {
            throw new ArgumentException("Identifier segment cannot be converted into a safe path segment.", nameof(value));
        }

        return safe;
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Path value must not be empty.", parameterName);
        }

        return value;
    }

    private static bool IsSubpath(string root, string candidate)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var normalizedRoot = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        return candidate.Equals(root, comparison)
            || candidate.StartsWith(normalizedRoot, comparison);
    }
}
