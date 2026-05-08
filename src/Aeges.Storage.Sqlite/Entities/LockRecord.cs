namespace Aeges.Storage.Sqlite.Entities;

/// <summary>
/// EF Core persistence record for a path-based runtime lock.
/// </summary>
internal sealed class LockRecord
{
    /// <summary>
    /// Gets or sets the lock identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the task that owns the lock.
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project where the lock applies.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the locked path pattern.
    /// </summary>
    public string PathPattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the lock was acquired.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the lock was released.
    /// </summary>
    public DateTimeOffset? ReleasedAt { get; set; }

    /// <summary>
    /// Gets or sets the owning task navigation.
    /// </summary>
    public TaskRecord? Task { get; set; }

    /// <summary>
    /// Gets or sets the related project navigation.
    /// </summary>
    public ProjectRecord? Project { get; set; }
}
