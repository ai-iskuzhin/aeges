namespace Aeges.Storage.Sqlite.Entities;

/// <summary>
/// EF Core persistence record for a short-token transport callback action.
/// </summary>
internal sealed class TransportCallbackActionRecord
{
    /// <summary>
    /// Gets or sets the short callback token.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the owning transport.
    /// </summary>
    public string Transport { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the transport-specific scope where the token is valid.
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stable action type.
    /// </summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON action payload.
    /// </summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the expiration timestamp.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the token was last used.
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>
    /// Gets or sets the number of times the token has been resolved.
    /// </summary>
    public int UseCount { get; set; }
}
