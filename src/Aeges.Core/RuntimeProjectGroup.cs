namespace Aeges.Core;

/// <summary>
/// Represents a classification group for related projects.
/// </summary>
public sealed class RuntimeProjectGroup
{
    private RuntimeProjectGroup(ProjectGroupId id, string name, string? path, DateTimeOffset createdAt)
    {
        Id = id;
        Name = RequireText(name, nameof(name));
        Path = NormalizeOptionalText(path);
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        IsArchived = false;
        ArchivedAt = null;
    }

    /// <summary>
    /// Gets the project group identifier.
    /// </summary>
    public ProjectGroupId Id { get; }

    /// <summary>
    /// Gets the human-readable group name.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the optional filesystem path that classifies projects into this group.
    /// </summary>
    public string? Path { get; private set; }

    /// <summary>
    /// Gets the timestamp when the group was registered.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the timestamp when the group was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the group is archived.
    /// </summary>
    public bool IsArchived { get; private set; }

    /// <summary>
    /// Gets the timestamp when the group was archived.
    /// </summary>
    public DateTimeOffset? ArchivedAt { get; private set; }

    /// <summary>
    /// Creates a project group.
    /// </summary>
    /// <param name="id">The project group identifier.</param>
    /// <param name="name">The human-readable group name.</param>
    /// <param name="createdAt">The registration timestamp.</param>
    /// <param name="path">The optional filesystem path used for scan classification.</param>
    /// <returns>A registered project group.</returns>
    public static RuntimeProjectGroup Create(
        ProjectGroupId id,
        string name,
        DateTimeOffset createdAt,
        string? path = null) =>
        new(id, name, path, createdAt);

    /// <summary>
    /// Rehydrates a project group from durable storage.
    /// </summary>
    /// <param name="id">The project group identifier.</param>
    /// <param name="name">The human-readable group name.</param>
    /// <param name="createdAt">The registration timestamp.</param>
    /// <param name="updatedAt">The last update timestamp.</param>
    /// <param name="path">The optional filesystem path used for scan classification.</param>
    /// <param name="isArchived">A value indicating whether the group is archived.</param>
    /// <param name="archivedAt">The archive timestamp.</param>
    /// <returns>A rehydrated project group.</returns>
    public static RuntimeProjectGroup Rehydrate(
        ProjectGroupId id,
        string name,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string? path = null,
        bool isArchived = false,
        DateTimeOffset? archivedAt = null)
    {
        if (isArchived && archivedAt is null)
        {
            throw new ArgumentException("Archived groups must include an archive timestamp.", nameof(archivedAt));
        }

        if (!isArchived && archivedAt is not null)
        {
            throw new ArgumentException("Active groups must not include an archive timestamp.", nameof(archivedAt));
        }

        return new RuntimeProjectGroup(id, name, path, createdAt)
        {
            UpdatedAt = updatedAt,
            IsArchived = isArchived,
            ArchivedAt = archivedAt,
        };
    }

    /// <summary>
    /// Updates group metadata.
    /// </summary>
    /// <param name="name">The new group name.</param>
    /// <param name="path">The optional filesystem path used for scan classification.</param>
    /// <param name="now">The update timestamp.</param>
    public void Update(string name, string? path, DateTimeOffset now)
    {
        Name = RequireText(name, nameof(name));
        Path = NormalizeOptionalText(path);
        UpdatedAt = now;
    }

    /// <summary>
    /// Archives the group without deleting its projects.
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

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
