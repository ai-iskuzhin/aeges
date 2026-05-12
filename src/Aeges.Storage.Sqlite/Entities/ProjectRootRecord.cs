namespace Aeges.Storage.Sqlite.Entities;

/// <summary>
/// EF Core persistence record for a project discovery root.
/// </summary>
internal sealed class ProjectRootRecord
{
    /// <summary>
    /// Gets or sets the project root identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project root display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the filesystem path to scan.
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
    /// Gets or sets a value indicating whether the root is archived.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the root was archived.
    /// </summary>
    public DateTimeOffset? ArchivedAt { get; set; }
}
