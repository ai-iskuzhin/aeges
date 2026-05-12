namespace Aeges.Storage.Sqlite.Entities;

/// <summary>
/// EF Core persistence record for a project group.
/// </summary>
internal sealed class ProjectGroupRecord
{
    /// <summary>
    /// Gets or sets the project group identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project group display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional filesystem path used for scan classification.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Gets or sets the registration timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the group is archived.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the group was archived.
    /// </summary>
    public DateTimeOffset? ArchivedAt { get; set; }

    /// <summary>
    /// Gets the projects classified by this group.
    /// </summary>
    public ICollection<ProjectRecord> Projects { get; } = [];
}
