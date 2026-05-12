using Aeges.Application.TelegramUsers;
using Aeges.Core;

namespace Aeges.Application.Tests;

public sealed class TelegramUserServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 12, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task EnsureAsync_bootstraps_first_chat_as_approved_admin()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var service = new TelegramUserService(unitOfWork, new FixedClock(Now));

        var authorization = await service.EnsureAsync(1001, CancellationToken.None);

        Assert.True(authorization.IsFirstAdmin);
        Assert.True(authorization.IsAdmin);
        Assert.True(authorization.IsApproved);
        Assert.Equal(TelegramUserStatus.Approved, authorization.User.Status);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task EnsureAsync_saves_observed_profile_for_new_user()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var service = new TelegramUserService(unitOfWork, new FixedClock(Now));

        var authorization = await service.EnsureAsync(
            1001,
            new RuntimeTelegramUserProfile("aigiz", "Aigiz", "Iskuzhin"),
            CancellationToken.None);

        Assert.Equal("aigiz", authorization.User.Username);
        Assert.Equal("Aigiz", authorization.User.FirstName);
        Assert.Equal("Iskuzhin", authorization.User.LastName);
    }

    [Fact]
    public async Task EnsureAsync_refreshes_observed_profile_for_existing_user()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var service = new TelegramUserService(unitOfWork, new FixedClock(Now));
        await service.EnsureAsync(
            1001,
            new RuntimeTelegramUserProfile("old", "Old", null),
            CancellationToken.None);

        var authorization = await service.EnsureAsync(
            1001,
            new RuntimeTelegramUserProfile("new", "New", "Name"),
            CancellationToken.None);

        Assert.Equal("new", authorization.User.Username);
        Assert.Equal("New", authorization.User.FirstName);
        Assert.Equal("Name", authorization.User.LastName);
        Assert.Equal(2, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task EnsureAsync_creates_later_chats_as_pending_users()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var service = new TelegramUserService(unitOfWork, new FixedClock(Now));
        await service.EnsureAsync(1001, CancellationToken.None);

        var authorization = await service.EnsureAsync(2002, CancellationToken.None);

        Assert.False(authorization.IsFirstAdmin);
        Assert.False(authorization.IsAdmin);
        Assert.False(authorization.IsApproved);
        Assert.Equal(TelegramUserStatus.Pending, authorization.User.Status);
        Assert.Equal(TelegramUserRole.User, authorization.User.Role);
    }

    [Fact]
    public async Task Access_grants_are_sparse_and_toggleable()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        var service = new TelegramUserService(unitOfWork, clock);
        await unitOfWork.Projects.AddAsync(
            RuntimeProject.Create(new ProjectId("aeges"), "Aeges", "/work/aeges", Now),
            CancellationToken.None);
        var authorization = await service.EnsureAsync(1001, CancellationToken.None);

        var empty = await service.GetAccessAsync(authorization.User.Id, CancellationToken.None);
        var granted = await service.SetProjectAccessAsync(
            authorization.User.Id,
            new ProjectId("aeges"),
            allowed: true,
            CancellationToken.None);
        var revoked = await service.SetProjectAccessAsync(
            authorization.User.Id,
            new ProjectId("aeges"),
            allowed: false,
            CancellationToken.None);

        Assert.True(empty.IsSuccess);
        Assert.Empty(empty.Value!.ProjectAccess);
        Assert.True(granted.IsSuccess);
        Assert.Single(granted.Value!.ProjectAccess);
        Assert.True(revoked.IsSuccess);
        Assert.Empty(revoked.Value!.ProjectAccess);
    }
}
