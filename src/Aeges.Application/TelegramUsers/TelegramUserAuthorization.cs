using Aeges.Core;

namespace Aeges.Application.TelegramUsers;

/// <summary>
/// Describes the authorization state for one inbound Telegram chat.
/// </summary>
/// <param name="User">The known Telegram user.</param>
/// <param name="IsFirstAdmin">A value indicating whether this request bootstrapped the first administrator.</param>
public sealed record TelegramUserAuthorization(
    RuntimeTelegramUser User,
    bool IsFirstAdmin)
{
    /// <summary>
    /// Gets a value indicating whether the user may operate the runtime.
    /// </summary>
    public bool IsApproved => User.Status == TelegramUserStatus.Approved;

    /// <summary>
    /// Gets a value indicating whether the user may administer Telegram access.
    /// </summary>
    public bool IsAdmin => User.Role == TelegramUserRole.Admin && IsApproved;
}
