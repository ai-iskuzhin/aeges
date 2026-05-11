namespace Aeges.Core;

/// <summary>
/// Represents a durable short-token callback action for constrained transports.
/// </summary>
public sealed class RuntimeTransportCallbackAction
{
    private RuntimeTransportCallbackAction(
        string token,
        string transport,
        string scope,
        string actionType,
        string payloadJson,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Token = RequireText(token, nameof(token));
        Transport = RequireText(transport, nameof(transport));
        Scope = RequireText(scope, nameof(scope));
        ActionType = RequireText(actionType, nameof(actionType));
        PayloadJson = RequireText(payloadJson, nameof(payloadJson));
        CreatedAt = createdAt;
        ExpiresAt = RequireExpiresAfterCreated(expiresAt, createdAt, nameof(expiresAt));
    }

    /// <summary>
    /// Gets the short callback token.
    /// </summary>
    public string Token { get; }

    /// <summary>
    /// Gets the transport that owns the callback token.
    /// </summary>
    public string Transport { get; }

    /// <summary>
    /// Gets the transport-specific scope where the token is valid.
    /// </summary>
    public string Scope { get; }

    /// <summary>
    /// Gets the stable action type.
    /// </summary>
    public string ActionType { get; }

    /// <summary>
    /// Gets the action payload as JSON.
    /// </summary>
    public string PayloadJson { get; }

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the expiration timestamp.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Gets the timestamp when the token was last used.
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; private set; }

    /// <summary>
    /// Gets the number of times the token has been resolved.
    /// </summary>
    public int UseCount { get; private set; }

    /// <summary>
    /// Creates a transport callback action.
    /// </summary>
    /// <param name="token">The short callback token.</param>
    /// <param name="transport">The transport that owns the token.</param>
    /// <param name="scope">The transport-specific scope where the token is valid.</param>
    /// <param name="actionType">The stable action type.</param>
    /// <param name="payloadJson">The action payload as JSON.</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="expiresAt">The expiration timestamp.</param>
    /// <returns>A durable transport callback action.</returns>
    public static RuntimeTransportCallbackAction Create(
        string token,
        string transport,
        string scope,
        string actionType,
        string payloadJson,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt) =>
        new(token, transport, scope, actionType, payloadJson, createdAt, expiresAt);

    /// <summary>
    /// Rehydrates a transport callback action from durable storage.
    /// </summary>
    /// <param name="token">The short callback token.</param>
    /// <param name="transport">The transport that owns the token.</param>
    /// <param name="scope">The transport-specific scope where the token is valid.</param>
    /// <param name="actionType">The stable action type.</param>
    /// <param name="payloadJson">The action payload as JSON.</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="expiresAt">The expiration timestamp.</param>
    /// <param name="lastUsedAt">The timestamp when the token was last used.</param>
    /// <param name="useCount">The number of times the token has been resolved.</param>
    /// <returns>A rehydrated transport callback action.</returns>
    public static RuntimeTransportCallbackAction Rehydrate(
        string token,
        string transport,
        string scope,
        string actionType,
        string payloadJson,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? lastUsedAt,
        int useCount)
    {
        if (useCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(useCount), useCount, "Use count must not be negative.");
        }

        return new RuntimeTransportCallbackAction(token, transport, scope, actionType, payloadJson, createdAt, expiresAt)
        {
            LastUsedAt = lastUsedAt,
            UseCount = useCount,
        };
    }

    /// <summary>
    /// Determines whether the token is expired at a timestamp.
    /// </summary>
    /// <param name="now">The timestamp to evaluate.</param>
    /// <returns><see langword="true"/> when the token is expired.</returns>
    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>
    /// Records that the token was resolved.
    /// </summary>
    /// <param name="now">The resolution timestamp.</param>
    public void MarkUsed(DateTimeOffset now)
    {
        LastUsedAt = now;
        UseCount++;
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value;
    }

    private static DateTimeOffset RequireExpiresAfterCreated(
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt,
        string parameterName)
    {
        if (expiresAt <= createdAt)
        {
            throw new ArgumentOutOfRangeException(parameterName, expiresAt, "Expiration must be after creation.");
        }

        return expiresAt;
    }
}
