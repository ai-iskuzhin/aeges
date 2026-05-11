namespace Aeges.Storage.Sqlite.Entities;

/// <summary>
/// EF Core persistence record for a registered project.
/// </summary>
internal sealed class ProjectRecord
{
    /// <summary>
    /// Gets or sets the project identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project root path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the registration timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the project is archived.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the project was archived.
    /// </summary>
    public DateTimeOffset? ArchivedAt { get; set; }

    /// <summary>
    /// Gets the tasks belonging to this project.
    /// </summary>
    public ICollection<TaskRecord> Tasks { get; } = [];

    /// <summary>
    /// Gets the locks currently or historically associated with this project.
    /// </summary>
    public ICollection<LockRecord> Locks { get; } = [];
}
