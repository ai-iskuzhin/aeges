namespace Aeges.Core;

/// <summary>
/// Represents a local filesystem root that can be scanned for projects.
/// </summary>
public sealed class RuntimeProjectRoot
{
    private RuntimeProjectRoot(ProjectRootId id, string name, string path, DateTimeOffset createdAt)
    {
        Id = id;
        Name = RequireText(name, nameof(name));
        Path = RequireText(path, nameof(path));
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        IsArchived = false;
        ArchivedAt = null;
    }

    /// <summary>
    /// Gets the project root identifier.
    /// </summary>
    public ProjectRootId Id { get; }

    /// <summary>
    /// Gets the human-readable root name.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the filesystem path to scan.
    /// </summary>
    public string Path { get; private set; }

    /// <summary>
    /// Gets the timestamp when the root was registered.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the timestamp when the root was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the root is archived.
    /// </summary>
    public bool IsArchived { get; private set; }

    /// <summary>
    /// Gets the timestamp when the root was archived.
    /// </summary>
    public DateTimeOffset? ArchivedAt { get; private set; }

    /// <summary>
    /// Creates a project discovery root.
    /// </summary>
    /// <param name="id">The project root identifier.</param>
    /// <param name="name">The human-readable root name.</param>
    /// <param name="path">The filesystem path to scan.</param>
    /// <param name="createdAt">The registration timestamp.</param>
    /// <returns>A registered project root.</returns>
    public static RuntimeProjectRoot Create(ProjectRootId id, string name, string path, DateTimeOffset createdAt) =>
        new(id, name, path, createdAt);

    /// <summary>
    /// Rehydrates a project discovery root from durable storage.
    /// </summary>
    /// <param name="id">The project root identifier.</param>
    /// <param name="name">The human-readable root name.</param>
    /// <param name="path">The filesystem path to scan.</param>
    /// <param name="createdAt">The registration timestamp.</param>
    /// <param name="updatedAt">The last update timestamp.</param>
    /// <param name="isArchived">A value indicating whether the root is archived.</param>
    /// <param name="archivedAt">The archive timestamp.</param>
    /// <returns>A rehydrated project root.</returns>
    public static RuntimeProjectRoot Rehydrate(
        ProjectRootId id,
        string name,
        string path,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        bool isArchived = false,
        DateTimeOffset? archivedAt = null)
    {
        if (isArchived && archivedAt is null)
        {
            throw new ArgumentException("Archived roots must include an archive timestamp.", nameof(archivedAt));
        }

        if (!isArchived && archivedAt is not null)
        {
            throw new ArgumentException("Active roots must not include an archive timestamp.", nameof(archivedAt));
        }

        return new RuntimeProjectRoot(id, name, path, createdAt)
        {
            UpdatedAt = updatedAt,
            IsArchived = isArchived,
            ArchivedAt = archivedAt,
        };
    }

    /// <summary>
    /// Updates root metadata.
    /// </summary>
    /// <param name="name">The new root name.</param>
    /// <param name="path">The filesystem path to scan.</param>
    /// <param name="now">The update timestamp.</param>
    public void Update(string name, string path, DateTimeOffset now)
    {
        Name = RequireText(name, nameof(name));
        Path = RequireText(path, nameof(path));
        UpdatedAt = now;
    }

    /// <summary>
    /// Archives the root without deleting discovered projects.
    /// </summary>
    /// <param name="now">The archive timestamp.</param>
    public void Archive(DateTimeOffset now)
    {
        if (IsArchived)
        {
            return;
        }

        IsArchived = true;
        ArchivedAt = now;
        UpdatedAt = now;
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value.Trim();
    }
}
