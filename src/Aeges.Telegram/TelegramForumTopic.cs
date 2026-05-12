namespace Aeges.Telegram;

/// <summary>
/// Describes a Telegram forum topic created for task-scoped interaction.
/// </summary>
/// <param name="MessageThreadId">The Telegram forum topic thread identifier.</param>
/// <param name="Name">The topic name.</param>
public sealed record TelegramForumTopic(
    int MessageThreadId,
    string Name);
