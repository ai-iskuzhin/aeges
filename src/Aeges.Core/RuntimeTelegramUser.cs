namespace Aeges.Core;

/// <summary>
/// Represents a Telegram chat/user known to the governed runtime.
/// </summary>
public sealed class RuntimeTelegramUser
{
    private RuntimeTelegramUser(
        TelegramUserId id,
        long chatId,
        RuntimeTelegramUserProfile profile,
        TelegramUserRole role,
        TelegramUserStatus status,
        DateTimeOffset createdAt)
    {
        if (chatId == 0)
        {
            throw new ArgumentException("Telegram chat identifier must not be zero.", nameof(chatId));
        }

        Id = id;
        ChatId = chatId;
        var normalizedProfile = profile.Normalize();
        Username = normalizedProfile.Username;
        FirstName = normalizedProfile.FirstName;
        LastName = normalizedProfile.LastName;
        Role = role;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    /// <summary>
    /// Gets the durable Telegram user identifier.
    /// </summary>
    public TelegramUserId Id { get; }

    /// <summary>
    /// Gets the Telegram chat identifier used for routing messages.
    /// </summary>
    public long ChatId { get; }

    /// <summary>
    /// Gets the Telegram username without an at-sign, when available.
    /// </summary>
    public string? Username { get; private set; }

    /// <summary>
    /// Gets the Telegram first name, when available.
    /// </summary>
    public string? FirstName { get; private set; }

    /// <summary>
    /// Gets the Telegram last name, when available.
    /// </summary>
    public string? LastName { get; private set; }

    /// <summary>
    /// Gets the user's local runtime role.
    /// </summary>
    public TelegramUserRole Role { get; private set; }

    /// <summary>
    /// Gets the user's local runtime access status.
    /// </summary>
    public TelegramUserStatus Status { get; private set; }

    /// <summary>
    /// Gets the timestamp when the user was first observed.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the timestamp when the user was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Creates the first Telegram administrator for a local runtime.
    /// </summary>
    /// <param name="chatId">The Telegram chat identifier.</param>
    /// <param name="now">The creation timestamp.</param>
    /// <param name="profile">The observed Telegram profile.</param>
    /// <returns>The approved administrator.</returns>
    public static RuntimeTelegramUser CreateFirstAdmin(
        long chatId,
        DateTimeOffset now,
        RuntimeTelegramUserProfile? profile = null) =>
        new(
            TelegramUserId.FromChatId(chatId),
            chatId,
            profile ?? RuntimeTelegramUserProfile.Empty,
            TelegramUserRole.Admin,
            TelegramUserStatus.Approved,
            now);

    /// <summary>
    /// Creates a pending Telegram user awaiting administrator approval.
    /// </summary>
    /// <param name="chatId">The Telegram chat identifier.</param>
    /// <param name="now">The creation timestamp.</param>
    /// <param name="profile">The observed Telegram profile.</param>
    /// <returns>The pending user.</returns>
    public static RuntimeTelegramUser CreatePending(
        long chatId,
        DateTimeOffset now,
        RuntimeTelegramUserProfile? profile = null) =>
        new(
            TelegramUserId.FromChatId(chatId),
            chatId,
            profile ?? RuntimeTelegramUserProfile.Empty,
            TelegramUserRole.User,
            TelegramUserStatus.Pending,
            now);

    /// <summary>
    /// Rehydrates a Telegram user from durable storage.
    /// </summary>
    /// <param name="id">The durable Telegram user identifier.</param>
    /// <param name="chatId">The Telegram chat identifier.</param>
    /// <param name="role">The user's role.</param>
    /// <param name="status">The user's status.</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="updatedAt">The last update timestamp.</param>
    /// <param name="profile">The observed Telegram profile.</param>
    /// <returns>The rehydrated Telegram user.</returns>
    public static RuntimeTelegramUser Rehydrate(
        TelegramUserId id,
        long chatId,
        TelegramUserRole role,
        TelegramUserStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        RuntimeTelegramUserProfile? profile = null) =>
        new(id, chatId, profile ?? RuntimeTelegramUserProfile.Empty, role, status, createdAt)
        {
            UpdatedAt = updatedAt,
        };

    /// <summary>
    /// Updates the cached Telegram profile when Telegram reports changed sender metadata.
    /// </summary>
    /// <param name="profile">The observed Telegram profile.</param>
    /// <param name="now">The update timestamp.</param>
    /// <returns><see langword="true"/> when any stored profile value changed.</returns>
    public bool UpdateProfile(RuntimeTelegramUserProfile profile, DateTimeOffset now)
    {
        var normalizedProfile = profile.Normalize();
        if (Username == normalizedProfile.Username &&
            FirstName == normalizedProfile.FirstName &&
            LastName == normalizedProfile.LastName)
        {
            return false;
        }

        Username = normalizedProfile.Username;
        FirstName = normalizedProfile.FirstName;
        LastName = normalizedProfile.LastName;
        UpdatedAt = now;

        return true;
    }

    /// <summary>
    /// Approves the user for runtime access.
    /// </summary>
    /// <param name="now">The update timestamp.</param>
    public void Approve(DateTimeOffset now)
    {
        Status = TelegramUserStatus.Approved;
        UpdatedAt = now;
    }

    /// <summary>
    /// Denies the user runtime access.
    /// </summary>
    /// <param name="now">The update timestamp.</param>
    public void Deny(DateTimeOffset now)
    {
        Status = TelegramUserStatus.Denied;
        UpdatedAt = now;
    }

    /// <summary>
    /// Sets the user's local runtime role.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <param name="now">The update timestamp.</param>
    public void SetRole(TelegramUserRole role, DateTimeOffset now)
    {
        Role = role;
        UpdatedAt = now;
    }
}
