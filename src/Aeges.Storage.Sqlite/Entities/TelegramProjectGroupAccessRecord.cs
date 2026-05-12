namespace Aeges.Storage.Sqlite.Entities;

/// <summary>
/// EF Core persistence record for a Telegram user's project group access grant.
/// </summary>
internal sealed class TelegramProjectGroupAccessRecord
{
    /// <summary>
    /// Gets or sets the Telegram user identifier.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project group identifier.
    /// </summary>
    public string ProjectGroupId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the grant creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the Telegram user.
    /// </summary>
    public TelegramUserRecord? User { get; set; }

    /// <summary>
    /// Gets or sets the project group.
    /// </summary>
    public ProjectGroupRecord? ProjectGroup { get; set; }
}
