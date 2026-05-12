namespace Aeges.Storage.Sqlite.Entities;

/// <summary>
/// EF Core persistence record for a Telegram user's project access grant.
/// </summary>
internal sealed class TelegramProjectAccessRecord
{
    /// <summary>
    /// Gets or sets the Telegram user identifier.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project identifier.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the grant creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the Telegram user.
    /// </summary>
    public TelegramUserRecord? User { get; set; }

    /// <summary>
    /// Gets or sets the project.
    /// </summary>
    public ProjectRecord? Project { get; set; }
}
