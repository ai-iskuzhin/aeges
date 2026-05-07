namespace Aeges.Core;

/// <summary>
/// Represents a path-based repository lock held by a runtime task.
/// </summary>
public sealed class RuntimeLock
{
    private RuntimeLock(
        LockId id,
        TaskId taskId,
        ProjectId projectId,
        string pathPattern,
        DateTimeOffset createdAt)
    {
        Id = id;
        TaskId = taskId;
        ProjectId = projectId;
        PathPattern = LockPathPattern.Require(pathPattern);
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Gets the lock identifier.
    /// </summary>
    public LockId Id { get; }

    /// <summary>
    /// Gets the task that owns the lock.
    /// </summary>
    public TaskId TaskId { get; }

    /// <summary>
    /// Gets the project where the lock applies.
    /// </summary>
    public ProjectId ProjectId { get; }

    /// <summary>
    /// Gets the relative path pattern protected by the lock.
    /// </summary>
    public string PathPattern { get; }

    /// <summary>
    /// Gets the timestamp when the lock was acquired.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the timestamp when the lock was released.
    /// </summary>
    public DateTimeOffset? ReleasedAt { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the lock is currently active.
    /// </summary>
    public bool IsActive => ReleasedAt is null;

    /// <summary>
    /// Acquires a new path-based lock.
    /// </summary>
    /// <param name="id">The lock identifier.</param>
    /// <param name="taskId">The task that owns the lock.</param>
    /// <param name="projectId">The project where the lock applies.</param>
    /// <param name="pathPattern">The relative path pattern protected by the lock.</param>
    /// <param name="createdAt">The acquisition timestamp.</param>
    /// <returns>An active runtime lock.</returns>
    public static RuntimeLock Acquire(
        LockId id,
        TaskId taskId,
        ProjectId projectId,
        string pathPattern,
        DateTimeOffset createdAt) =>
        new(id, taskId, projectId, pathPattern, createdAt);

    /// <summary>
    /// Rehydrates a path-based lock from durable storage.
    /// </summary>
    /// <param name="id">The lock identifier.</param>
    /// <param name="taskId">The task that owns the lock.</param>
    /// <param name="projectId">The project where the lock applies.</param>
    /// <param name="pathPattern">The relative path pattern protected by the lock.</param>
    /// <param name="createdAt">The acquisition timestamp.</param>
    /// <param name="releasedAt">The release timestamp, when the lock has been released.</param>
    /// <returns>A rehydrated runtime lock.</returns>
    public static RuntimeLock Rehydrate(
        LockId id,
        TaskId taskId,
        ProjectId projectId,
        string pathPattern,
        DateTimeOffset createdAt,
        DateTimeOffset? releasedAt)
    {
        var runtimeLock = new RuntimeLock(id, taskId, projectId, pathPattern, createdAt)
        {
            ReleasedAt = releasedAt,
        };

        return runtimeLock;
    }

    /// <summary>
    /// Releases the lock.
    /// </summary>
    /// <param name="now">The release timestamp.</param>
    public void Release(DateTimeOffset now)
    {
        if (!IsActive)
        {
            throw new AegesDomainException($"Lock '{Id}' has already been released.");
        }

        ReleasedAt = now;
    }

    /// <summary>
    /// Determines whether this lock protects a relative path.
    /// </summary>
    /// <param name="relativePath">The relative path to check.</param>
    /// <returns><see langword="true"/> when the lock pattern protects the path; otherwise <see langword="false"/>.</returns>
    public bool ProtectsPath(string relativePath) =>
        LockPathPattern.Matches(PathPattern, relativePath);

    /// <summary>
    /// Determines whether this lock conflicts with another active lock.
    /// </summary>
    /// <param name="other">The other lock to compare.</param>
    /// <returns><see langword="true"/> when the locks conflict; otherwise <see langword="false"/>.</returns>
    public bool ConflictsWith(RuntimeLock other)
    {
        if (ProjectId != other.ProjectId || TaskId == other.TaskId || !IsActive || !other.IsActive)
        {
            return false;
        }

        return LockPathPattern.Overlaps(PathPattern, other.PathPattern);
    }
}

internal static class LockPathPattern
{
    public static string Require(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Lock path pattern must not be empty.", nameof(value));
        }

        if (Path.IsPathRooted(value) || HasWindowsRoot(value))
        {
            throw new ArgumentException("Lock path pattern must be relative.", nameof(value));
        }

        var segments = value.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

        if (segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("Lock path pattern must not contain current or parent directory segments.", nameof(value));
        }

        return Normalize(value);
    }

    public static bool Matches(string pattern, string relativePath) =>
        System.Text.RegularExpressions.Regex.IsMatch(
            Require(relativePath),
            ToRegexPattern(Require(pattern)),
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    public static bool Overlaps(string leftPattern, string rightPattern)
    {
        var normalizedLeft = Normalize(leftPattern);
        var normalizedRight = Normalize(rightPattern);

        return normalizedLeft == normalizedRight
            || Matches(normalizedLeft, CreateRepresentativePath(normalizedRight))
            || Matches(normalizedRight, CreateRepresentativePath(normalizedLeft));
    }

    private static string Normalize(string value)
    {
        var normalized = value.Replace('\\', '/');

        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return string.Join("/", segments);
    }

    private static string ToRegexPattern(string pattern)
    {
        var escaped = System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*\\*", ".*", StringComparison.Ordinal)
            .Replace("\\*", "[^/]*", StringComparison.Ordinal);

        return "^" + escaped + "$";
    }

    private static string CreateRepresentativePath(string pattern)
    {
        var segments = pattern
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment switch
            {
                "**" => "sample/deep",
                "*" => "sample",
                _ => segment.Replace("**", "sample/deep", StringComparison.Ordinal)
                    .Replace("*", "sample", StringComparison.Ordinal),
            });

        return string.Join("/", segments);
    }

    private static bool HasWindowsRoot(string value) =>
        value.StartsWith(@"\\", StringComparison.Ordinal)
        || (value.Length >= 3 && char.IsAsciiLetter(value[0]) && value[1] == ':' && value[2] is '\\' or '/');
}
