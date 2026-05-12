using Aeges.Application.Projects;
using Aeges.Core;

namespace Aeges.Application.Tests;

public sealed class ProjectGroupServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 12, 08, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task RegisterAsync_creates_project_group_and_saves_changes()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var service = new ProjectGroupService(unitOfWork, new FixedClock(Now));

        var result = await service.RegisterAsync(
            new RegisterProjectGroupRequest("Analitex", "/work/analitex", new ProjectGroupId("analitex")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(new ProjectGroupId("analitex"), result.Value.Id);
        Assert.Equal("Analitex", result.Value.Name);
        Assert.Equal("/work/analitex", result.Value.Path);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task GetAsync_returns_project_group_or_failure()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var service = new ProjectGroupService(unitOfWork, new FixedClock(Now));
        await service.RegisterAsync(
            new RegisterProjectGroupRequest("Analitex", "/work/analitex", new ProjectGroupId("analitex")),
            CancellationToken.None);

        var existing = await service.GetAsync(new ProjectGroupId("analitex"), CancellationToken.None);
        var missing = await service.GetAsync(new ProjectGroupId("missing"), CancellationToken.None);

        Assert.True(existing.IsSuccess);
        Assert.False(missing.IsSuccess);
        Assert.Equal("project_group_not_found", missing.Error?.Code);
    }
}
