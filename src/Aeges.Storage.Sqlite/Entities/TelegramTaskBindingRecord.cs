namespace Aeges.Storage.Sqlite.Entities;

/// <summary>
/// EF Core persistence record for routing task updates back to Telegram.
/// </summary>
internal sealed class TelegramTaskBindingRecord
{
    /// <summary>
    /// Gets or sets the Telegram chat identifier.
    /// </summary>
    public long ChatId { get; set; }

    /// <summary>
    /// Gets or sets the Telegram forum topic identifier, when available.
    /// </summary>
    public int MessageThreadId { get; set; }

    /// <summary>
    /// Gets or sets the task identifier.
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the latest editable task details message identifier.
    /// </summary>
    public int? DetailMessageId { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the related task.
    /// </summary>
    public TaskRecord? Task { get; set; }
}
