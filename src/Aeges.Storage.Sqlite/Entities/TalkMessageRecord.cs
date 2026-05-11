namespace Aeges.Storage.Sqlite.Entities;

/// <summary>
/// EF Core persistence record for a governed discussion message.
/// </summary>
internal sealed class TalkMessageRecord
{
    /// <summary>
    /// Gets or sets the talk message identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the owning talk session identifier.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message role.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the owning talk session.
    /// </summary>
    public TalkSessionRecord? Session { get; set; }
}
