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

        return value;
    }

    private static bool HasWindowsRoot(string value) =>
        value.StartsWith(@"\\", StringComparison.Ordinal)
        || (value.Length >= 3 && char.IsAsciiLetter(value[0]) && value[1] == ':' && value[2] is '\\' or '/');
}
