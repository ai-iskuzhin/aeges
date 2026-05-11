namespace Aeges.Core;

/// <summary>
/// Represents one durable message in a governed discussion session.
/// </summary>
public sealed class RuntimeTalkMessage
{
    private RuntimeTalkMessage(
        TalkMessageId id,
        TalkSessionId sessionId,
        TalkMessageRole role,
        string content,
        DateTimeOffset createdAt)
    {
        Id = id;
        SessionId = sessionId;
        Role = role;
        Content = RequireText(content, nameof(content));
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Gets the talk message identifier.
    /// </summary>
    public TalkMessageId Id { get; }

    /// <summary>
    /// Gets the session that owns the message.
    /// </summary>
    public TalkSessionId SessionId { get; }

    /// <summary>
    /// Gets the actor that produced the message.
    /// </summary>
    public TalkMessageRole Role { get; }

    /// <summary>
    /// Gets the message content.
    /// </summary>
    public string Content { get; }

    /// <summary>
    /// Gets the message creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Creates a durable discussion message.
    /// </summary>
    /// <param name="id">The message identifier.</param>
    /// <param name="sessionId">The owning session identifier.</param>
    /// <param name="role">The actor that produced the message.</param>
    /// <param name="content">The message content.</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <returns>A durable talk message.</returns>
    public static RuntimeTalkMessage Create(
        TalkMessageId id,
        TalkSessionId sessionId,
        TalkMessageRole role,
        string content,
        DateTimeOffset createdAt) =>
        new(id, sessionId, role, content, createdAt);

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value;
    }
}
