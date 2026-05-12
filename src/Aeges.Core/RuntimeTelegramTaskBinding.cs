namespace Aeges.Core;

/// <summary>
/// Records where Telegram should route updates for a runtime task.
/// </summary>
public sealed class RuntimeTelegramTaskBinding
{
    private RuntimeTelegramTaskBinding(
        long chatId,
        int? messageThreadId,
        TaskId taskId,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        ChatId = chatId;
        MessageThreadId = messageThreadId;
        TaskId = taskId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Gets the Telegram chat identifier that owns the route.
    /// </summary>
    public long ChatId { get; }

    /// <summary>
    /// Gets the Telegram forum topic identifier, or <see langword="null"/> for private chats and non-topic chats.
    /// </summary>
    public int? MessageThreadId { get; }

    /// <summary>
    /// Gets the task routed to the Telegram chat or topic.
    /// </summary>
    public TaskId TaskId { get; }

    /// <summary>
    /// Gets the latest bot message that can be edited as task details.
    /// </summary>
    public int? DetailMessageId { get; private set; }

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the last update timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Creates a Telegram task binding.
    /// </summary>
    /// <param name="chatId">The Telegram chat identifier.</param>
    /// <param name="messageThreadId">The Telegram forum topic identifier, when available.</param>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="detailMessageId">The latest editable task details message, when available.</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <returns>A Telegram task binding.</returns>
    public static RuntimeTelegramTaskBinding Create(
        long chatId,
        int? messageThreadId,
        TaskId taskId,
        int? detailMessageId,
        DateTimeOffset createdAt)
    {
        var binding = new RuntimeTelegramTaskBinding(chatId, messageThreadId, taskId, createdAt, createdAt);
        binding.RecordDetailMessage(detailMessageId, createdAt);

        return binding;
    }

    /// <summary>
    /// Rehydrates a Telegram task binding from durable storage.
    /// </summary>
    /// <param name="chatId">The Telegram chat identifier.</param>
    /// <param name="messageThreadId">The Telegram forum topic identifier, when available.</param>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="detailMessageId">The latest editable task details message, when available.</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="updatedAt">The last update timestamp.</param>
    /// <returns>A rehydrated Telegram task binding.</returns>
    public static RuntimeTelegramTaskBinding Rehydrate(
        long chatId,
        int? messageThreadId,
        TaskId taskId,
        int? detailMessageId,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) =>
        new(chatId, messageThreadId, taskId, createdAt, updatedAt)
        {
            DetailMessageId = detailMessageId,
        };

    /// <summary>
    /// Records the latest task details message for this binding.
    /// </summary>
    /// <param name="messageId">The editable task details message identifier, when available.</param>
    /// <param name="updatedAt">The update timestamp.</param>
    public void RecordDetailMessage(int? messageId, DateTimeOffset updatedAt)
    {
        DetailMessageId = messageId;
        UpdatedAt = updatedAt;
    }
}
