namespace Aeges.Core;

/// <summary>
/// Represents a durable governed discussion session with a coding agent.
/// </summary>
public sealed class RuntimeTalkSession
{
    private RuntimeTalkSession(
        TalkSessionId id,
        string source,
        string title,
        RunnerId runnerId,
        DateTimeOffset createdAt)
    {
        Id = id;
        Source = RequireText(source, nameof(source));
        Title = RequireText(title, nameof(title));
        RunnerId = runnerId;
        Status = TalkSessionStatus.Open;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    /// <summary>
    /// Gets the talk session identifier.
    /// </summary>
    public TalkSessionId Id { get; }

    /// <summary>
    /// Gets the stable source that owns continuity for this session.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Gets the human-readable session title.
    /// </summary>
    public string Title { get; private set; }

    /// <summary>
    /// Gets the runner assigned to answer this session.
    /// </summary>
    public RunnerId RunnerId { get; }

    /// <summary>
    /// Gets the discussion lifecycle status.
    /// </summary>
    public TalkSessionStatus Status { get; private set; }

    /// <summary>
    /// Gets the external runner session identifier, when the runner exposes one.
    /// </summary>
    public string? ExternalSessionId { get; private set; }

    /// <summary>
    /// Gets the timestamp when the session was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the timestamp when the session was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Gets the timestamp when the session was archived.
    /// </summary>
    public DateTimeOffset? ArchivedAt { get; private set; }

    /// <summary>
    /// Creates a new open talk session.
    /// </summary>
    /// <param name="id">The talk session identifier.</param>
    /// <param name="source">The stable source that owns session continuity.</param>
    /// <param name="title">The human-readable session title.</param>
    /// <param name="runnerId">The runner assigned to answer the session.</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <returns>A new talk session.</returns>
    public static RuntimeTalkSession Create(
        TalkSessionId id,
        string source,
        string title,
        RunnerId runnerId,
        DateTimeOffset createdAt) =>
        new(id, source, title, runnerId, createdAt);

    /// <summary>
    /// Rehydrates a talk session from durable storage.
    /// </summary>
    /// <param name="id">The talk session identifier.</param>
    /// <param name="source">The stable source that owns session continuity.</param>
    /// <param name="title">The human-readable session title.</param>
    /// <param name="runnerId">The runner assigned to answer the session.</param>
    /// <param name="status">The discussion lifecycle status.</param>
    /// <param name="externalSessionId">The external runner session identifier.</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="updatedAt">The last update timestamp.</param>
    /// <param name="archivedAt">The archive timestamp.</param>
    /// <returns>A rehydrated talk session.</returns>
    public static RuntimeTalkSession Rehydrate(
        TalkSessionId id,
        string source,
        string title,
        RunnerId runnerId,
        TalkSessionStatus status,
        string? externalSessionId,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? archivedAt)
    {
        if (status == TalkSessionStatus.Archived && archivedAt is null)
        {
            throw new ArgumentException("Archived talk sessions must include an archive timestamp.", nameof(archivedAt));
        }

        if (status == TalkSessionStatus.Open && archivedAt is not null)
        {
            throw new ArgumentException("Open talk sessions must not include an archive timestamp.", nameof(archivedAt));
        }

        return new RuntimeTalkSession(id, source, title, runnerId, createdAt)
        {
            Status = status,
            ExternalSessionId = RequireOptionalText(externalSessionId, nameof(externalSessionId)),
            UpdatedAt = updatedAt,
            ArchivedAt = archivedAt,
        };
    }

    /// <summary>
    /// Records that the discussion received a new message.
    /// </summary>
    /// <param name="now">The update timestamp.</param>
    public void Touch(DateTimeOffset now)
    {
        EnsureOpen();
        UpdatedAt = now;
    }

    /// <summary>
    /// Stores the external runner session identifier returned by a runner.
    /// </summary>
    /// <param name="externalSessionId">The external runner session identifier.</param>
    /// <param name="now">The update timestamp.</param>
    public void RecordExternalSessionId(string externalSessionId, DateTimeOffset now)
    {
        EnsureOpen();
        ExternalSessionId = RequireText(externalSessionId, nameof(externalSessionId));
        UpdatedAt = now;
    }

    /// <summary>
    /// Archives the session so no further messages are accepted.
    /// </summary>
    /// <param name="now">The archive timestamp.</param>
    public void Archive(DateTimeOffset now)
    {
        if (Status == TalkSessionStatus.Archived)
        {
            return;
        }

        Status = TalkSessionStatus.Archived;
        ArchivedAt = now;
        UpdatedAt = now;
    }

    private void EnsureOpen()
    {
        if (Status != TalkSessionStatus.Open)
        {
            throw new AegesDomainException($"Talk session '{Id}' is archived.");
        }
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value;
    }

    private static string? RequireOptionalText(string? value, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        return RequireText(value, parameterName);
    }
}
