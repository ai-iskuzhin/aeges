namespace Aeges.Storage.Sqlite.Entities;

/// <summary>
/// EF Core persistence record for a governed discussion session.
/// </summary>
internal sealed class TalkSessionRecord
{
    /// <summary>
    /// Gets or sets the talk session identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stable source that owns session continuity.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable session title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the runner identifier.
    /// </summary>
    public string RunnerId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the session lifecycle status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the external runner session identifier.
    /// </summary>
    public string? ExternalSessionId { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the archive timestamp.
    /// </summary>
    public DateTimeOffset? ArchivedAt { get; set; }

    /// <summary>
    /// Gets the messages belonging to this session.
    /// </summary>
    public ICollection<TalkMessageRecord> Messages { get; } = [];
}
