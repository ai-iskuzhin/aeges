using Aeges.Core;

namespace Aeges.Storage;

/// <summary>
/// Provides persistence operations for Telegram users and their access grants.
/// </summary>
public interface ITelegramUserRepository
{
    /// <summary>
    /// Counts known Telegram users.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The number of known Telegram users.</returns>
    Task<int> CountAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Adds a Telegram user to storage.
    /// </summary>
    /// <param name="user">The user to add.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(RuntimeTelegramUser user, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a Telegram user by chat identifier.
    /// </summary>
    /// <param name="chatId">The Telegram chat identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching user, or <see langword="null"/> when none exists.</returns>
    Task<RuntimeTelegramUser?> GetByChatIdAsync(long chatId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a Telegram user by durable identifier.
    /// </summary>
    /// <param name="userId">The Telegram user identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching user, or <see langword="null"/> when none exists.</returns>
    Task<RuntimeTelegramUser?> GetByIdAsync(TelegramUserId userId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists known Telegram users.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The known users.</returns>
    Task<IReadOnlyList<RuntimeTelegramUser>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Updates a Telegram user.
    /// </summary>
    /// <param name="user">The user to update.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(RuntimeTelegramUser user, CancellationToken cancellationToken);

    /// <summary>
    /// Lists project grants for a Telegram user.
    /// </summary>
    /// <param name="userId">The Telegram user identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The project grants.</returns>
    Task<IReadOnlyList<RuntimeTelegramProjectAccess>> ListProjectAccessAsync(
        TelegramUserId userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists project group grants for a Telegram user.
    /// </summary>
    /// <param name="userId">The Telegram user identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The project group grants.</returns>
    Task<IReadOnlyList<RuntimeTelegramProjectGroupAccess>> ListProjectGroupAccessAsync(
        TelegramUserId userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sets whether a Telegram user may access one project.
    /// </summary>
    /// <param name="userId">The Telegram user identifier.</param>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="allowed">A value indicating whether access is granted.</param>
    /// <param name="createdAt">The grant timestamp when access is enabled.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetProjectAccessAsync(
        TelegramUserId userId,
        ProjectId projectId,
        bool allowed,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sets whether a Telegram user may access one project group.
    /// </summary>
    /// <param name="userId">The Telegram user identifier.</param>
    /// <param name="projectGroupId">The project group identifier.</param>
    /// <param name="allowed">A value indicating whether access is granted.</param>
    /// <param name="createdAt">The grant timestamp when access is enabled.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetProjectGroupAccessAsync(
        TelegramUserId userId,
        ProjectGroupId projectGroupId,
        bool allowed,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken);
}
