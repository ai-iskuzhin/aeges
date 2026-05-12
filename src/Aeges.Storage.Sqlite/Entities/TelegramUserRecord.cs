using Aeges.Core;

namespace Aeges.Storage.Sqlite.Entities;

/// <summary>
/// EF Core persistence record for a Telegram user known to the runtime.
/// </summary>
internal sealed class TelegramUserRecord
{
    /// <summary>
    /// Gets or sets the durable Telegram user identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Telegram chat identifier.
    /// </summary>
    public long ChatId { get; set; }

    /// <summary>
    /// Gets or sets the Telegram username without an at-sign, when available.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the Telegram first name, when available.
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Gets or sets the Telegram last name, when available.
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Gets or sets the user's role.
    /// </summary>
    public TelegramUserRole Role { get; set; }

    /// <summary>
    /// Gets or sets the user's access status.
    /// </summary>
    public TelegramUserStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the user was first observed.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the user was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets the project access grants for this user.
    /// </summary>
    public ICollection<TelegramProjectAccessRecord> ProjectAccess { get; } = [];

    /// <summary>
    /// Gets the project group access grants for this user.
    /// </summary>
    public ICollection<TelegramProjectGroupAccessRecord> ProjectGroupAccess { get; } = [];
}
