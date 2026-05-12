namespace Aeges.Core;

/// <summary>
/// Represents an explicit Telegram user grant for one project.
/// </summary>
/// <param name="UserId">The Telegram user identifier.</param>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="CreatedAt">The grant creation timestamp.</param>
public sealed record RuntimeTelegramProjectAccess(
    TelegramUserId UserId,
    ProjectId ProjectId,
    DateTimeOffset CreatedAt);
