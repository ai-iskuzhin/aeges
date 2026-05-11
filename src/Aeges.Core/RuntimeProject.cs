namespace Aeges.Core;

/// <summary>
/// Represents a project registered for governed runtime execution.
/// </summary>
public sealed class RuntimeProject
{
    private RuntimeProject(ProjectId id, string name, string path, DateTimeOffset createdAt)
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
    /// Gets the project identifier.
    /// </summary>
    public ProjectId Id { get; }

    /// <summary>
    /// Gets the human-readable project name.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the project root path.
    /// </summary>
    public string Path { get; private set; }

    /// <summary>
    /// Gets the timestamp when the project was registered.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the timestamp when the project was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the project is archived and unavailable for new work.
    /// </summary>
    public bool IsArchived { get; private set; }

    /// <summary>
    /// Gets the timestamp when the project was archived.
    /// </summary>
    public DateTimeOffset? ArchivedAt { get; private set; }

    /// <summary>
    /// Creates a new registered project.
    /// </summary>
    /// <param name="id">The project identifier.</param>
    /// <param name="name">The human-readable project name.</param>
    /// <param name="path">The project root path.</param>
    /// <param name="createdAt">The registration timestamp.</param>
    /// <returns>A registered runtime project.</returns>
    public static RuntimeProject Create(ProjectId id, string name, string path, DateTimeOffset createdAt) =>
        new(id, name, path, createdAt);

    /// <summary>
    /// Rehydrates a registered project from durable storage.
    /// </summary>
    /// <param name="id">The project identifier.</param>
    /// <param name="name">The human-readable project name.</param>
    /// <param name="path">The project root path.</param>
    /// <param name="createdAt">The registration timestamp.</param>
    /// <param name="updatedAt">The last update timestamp.</param>
    /// <param name="isArchived">A value indicating whether the project is archived.</param>
    /// <param name="archivedAt">The archive timestamp.</param>
    /// <returns>A rehydrated runtime project.</returns>
    public static RuntimeProject Rehydrate(
        ProjectId id,
        string name,
        string path,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        bool isArchived = false,
        DateTimeOffset? archivedAt = null)
    {
        if (isArchived && archivedAt is null)
        {
            throw new ArgumentException("Archived projects must include an archive timestamp.", nameof(archivedAt));
        }

        if (!isArchived && archivedAt is not null)
        {
            throw new ArgumentException("Active projects must not include an archive timestamp.", nameof(archivedAt));
        }

        var project = new RuntimeProject(id, name, path, createdAt)
        {
            UpdatedAt = updatedAt,
            IsArchived = isArchived,
            ArchivedAt = archivedAt,
        };

        return project;
    }

    /// <summary>
    /// Updates project metadata.
    /// </summary>
    /// <param name="name">The new project name.</param>
    /// <param name="path">The new project root path.</param>
    /// <param name="now">The update timestamp.</param>
    public void Update(string name, string path, DateTimeOffset now)
    {
        Name = RequireText(name, nameof(name));
        Path = RequireText(path, nameof(path));
        UpdatedAt = now;
    }

    /// <summary>
    /// Archives the project so its history remains visible but no new tasks should be created for it.
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

        return value;
    }
}
