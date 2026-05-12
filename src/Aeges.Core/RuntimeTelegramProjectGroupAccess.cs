namespace Aeges.Core;

/// <summary>
/// Represents an explicit Telegram user grant for one project group.
/// </summary>
/// <param name="UserId">The Telegram user identifier.</param>
/// <param name="ProjectGroupId">The project group identifier.</param>
/// <param name="CreatedAt">The grant creation timestamp.</param>
public sealed record RuntimeTelegramProjectGroupAccess(
    TelegramUserId UserId,
    ProjectGroupId ProjectGroupId,
    DateTimeOffset CreatedAt);
