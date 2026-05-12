using Aeges.Core;
using Aeges.Storage;

namespace Aeges.Application.TelegramUsers;

/// <summary>
/// Coordinates Telegram user bootstrap, approval, and project access grants.
/// </summary>
public sealed class TelegramUserService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramUserService"/> class.
    /// </summary>
    /// <param name="unitOfWork">The persistence unit of work.</param>
    /// <param name="clock">The runtime clock.</param>
    public TelegramUserService(IUnitOfWork unitOfWork, IClock clock)
    {
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    /// <summary>
    /// Ensures an inbound Telegram chat has a durable user record.
    /// </summary>
    /// <param name="chatId">The Telegram chat identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The authorization state for the chat.</returns>
    public async Task<TelegramUserAuthorization> EnsureAsync(
        long chatId,
        CancellationToken cancellationToken) =>
        await EnsureAsync(chatId, RuntimeTelegramUserProfile.Empty, cancellationToken);

    /// <summary>
    /// Ensures an inbound Telegram chat has a durable user record and updates its observed profile.
    /// </summary>
    /// <param name="chatId">The Telegram chat identifier.</param>
    /// <param name="profile">The observed Telegram profile.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The authorization state for the chat.</returns>
    public async Task<TelegramUserAuthorization> EnsureAsync(
        long chatId,
        RuntimeTelegramUserProfile profile,
        CancellationToken cancellationToken)
    {
        var existing = await unitOfWork.TelegramUsers.GetByChatIdAsync(chatId, cancellationToken);

        if (existing is not null)
        {
            if (!profile.IsEmpty && existing.UpdateProfile(profile, clock.Now))
            {
                await unitOfWork.TelegramUsers.UpdateAsync(existing, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return new TelegramUserAuthorization(existing, IsFirstAdmin: false);
        }

        var userCount = await unitOfWork.TelegramUsers.CountAsync(cancellationToken);
        var now = clock.Now;
        var user = userCount == 0
            ? RuntimeTelegramUser.CreateFirstAdmin(chatId, now, profile)
            : RuntimeTelegramUser.CreatePending(chatId, now, profile);

        await unitOfWork.TelegramUsers.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new TelegramUserAuthorization(user, IsFirstAdmin: user.Role == TelegramUserRole.Admin);
    }

    /// <summary>
    /// Lists known Telegram users.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The known users.</returns>
    public async Task<IReadOnlyList<RuntimeTelegramUser>> ListAsync(CancellationToken cancellationToken) =>
        await unitOfWork.TelegramUsers.ListAsync(cancellationToken);

    /// <summary>
    /// Gets a Telegram user access snapshot.
    /// </summary>
    /// <param name="userId">The Telegram user identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The access snapshot, or an expected failure.</returns>
    public async Task<ApplicationResult<TelegramUserAccessSnapshot>> GetAccessAsync(
        TelegramUserId userId,
        CancellationToken cancellationToken)
    {
        var user = await unitOfWork.TelegramUsers.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return ApplicationResult<TelegramUserAccessSnapshot>.Failure(
                "telegram_user_not_found",
                $"Telegram user '{userId}' was not found.");
        }

        var projectAccess = await unitOfWork.TelegramUsers.ListProjectAccessAsync(userId, cancellationToken);
        var groupAccess = await unitOfWork.TelegramUsers.ListProjectGroupAccessAsync(userId, cancellationToken);

        return ApplicationResult<TelegramUserAccessSnapshot>.Success(
            new TelegramUserAccessSnapshot(user, projectAccess, groupAccess));
    }

    /// <summary>
    /// Approves a Telegram user.
    /// </summary>
    /// <param name="userId">The Telegram user identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated user, or an expected failure.</returns>
    public async Task<ApplicationResult<RuntimeTelegramUser>> ApproveAsync(
        TelegramUserId userId,
        CancellationToken cancellationToken)
    {
        var user = await unitOfWork.TelegramUsers.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return ApplicationResult<RuntimeTelegramUser>.Failure(
                "telegram_user_not_found",
                $"Telegram user '{userId}' was not found.");
        }

        user.Approve(clock.Now);
        await unitOfWork.TelegramUsers.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RuntimeTelegramUser>.Success(user);
    }

    /// <summary>
    /// Denies a Telegram user.
    /// </summary>
    /// <param name="userId">The Telegram user identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated user, or an expected failure.</returns>
    public async Task<ApplicationResult<RuntimeTelegramUser>> DenyAsync(
        TelegramUserId userId,
        CancellationToken cancellationToken)
    {
        var user = await unitOfWork.TelegramUsers.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return ApplicationResult<RuntimeTelegramUser>.Failure(
                "telegram_user_not_found",
                $"Telegram user '{userId}' was not found.");
        }

        user.Deny(clock.Now);
        await unitOfWork.TelegramUsers.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RuntimeTelegramUser>.Success(user);
    }

    /// <summary>
    /// Sets a project grant for a Telegram user.
    /// </summary>
    /// <param name="userId">The Telegram user identifier.</param>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="allowed">A value indicating whether access is granted.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated access snapshot, or an expected failure.</returns>
    public async Task<ApplicationResult<TelegramUserAccessSnapshot>> SetProjectAccessAsync(
        TelegramUserId userId,
        ProjectId projectId,
        bool allowed,
        CancellationToken cancellationToken)
    {
        if (await unitOfWork.Projects.GetByIdAsync(projectId, cancellationToken) is null)
        {
            return ApplicationResult<TelegramUserAccessSnapshot>.Failure(
                "project_not_found",
                $"Project '{projectId}' was not found.");
        }

        await unitOfWork.TelegramUsers.SetProjectAccessAsync(userId, projectId, allowed, clock.Now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAccessAsync(userId, cancellationToken);
    }

    /// <summary>
    /// Sets a project group grant for a Telegram user.
    /// </summary>
    /// <param name="userId">The Telegram user identifier.</param>
    /// <param name="projectGroupId">The project group identifier.</param>
    /// <param name="allowed">A value indicating whether access is granted.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated access snapshot, or an expected failure.</returns>
    public async Task<ApplicationResult<TelegramUserAccessSnapshot>> SetProjectGroupAccessAsync(
        TelegramUserId userId,
        ProjectGroupId projectGroupId,
        bool allowed,
        CancellationToken cancellationToken)
    {
        if (await unitOfWork.ProjectGroups.GetByIdAsync(projectGroupId, cancellationToken) is null)
        {
            return ApplicationResult<TelegramUserAccessSnapshot>.Failure(
                "project_group_not_found",
                $"Project group '{projectGroupId}' was not found.");
        }

        await unitOfWork.TelegramUsers.SetProjectGroupAccessAsync(userId, projectGroupId, allowed, clock.Now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAccessAsync(userId, cancellationToken);
    }
}
