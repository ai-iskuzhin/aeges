using Aeges.Core;

namespace Aeges.Application.TelegramUsers;

/// <summary>
/// Describes one Telegram user's explicit project and group access grants.
/// </summary>
/// <param name="User">The Telegram user.</param>
/// <param name="ProjectAccess">The explicit project grants.</param>
/// <param name="ProjectGroupAccess">The explicit project group grants.</param>
public sealed record TelegramUserAccessSnapshot(
    RuntimeTelegramUser User,
    IReadOnlyList<RuntimeTelegramProjectAccess> ProjectAccess,
    IReadOnlyList<RuntimeTelegramProjectGroupAccess> ProjectGroupAccess);
