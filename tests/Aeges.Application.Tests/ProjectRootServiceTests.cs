using Aeges.Application.Projects;
using Aeges.Core;

namespace Aeges.Application.Tests;

public sealed class ProjectRootServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 12, 08, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task RegisterAsync_creates_project_root_and_saves_changes()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var service = new ProjectRootService(unitOfWork, new FixedClock(Now));

        var result = await service.RegisterAsync(
            new RegisterProjectRootRequest("Work", "/work", new ProjectRootId("work")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(new ProjectRootId("work"), result.Value.Id);
        Assert.Equal("Work", result.Value.Name);
        Assert.Equal("/work", result.Value.Path);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task GetAsync_returns_project_root_or_failure()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var service = new ProjectRootService(unitOfWork, new FixedClock(Now));
        await service.RegisterAsync(
            new RegisterProjectRootRequest("Work", "/work", new ProjectRootId("work")),
            CancellationToken.None);

        var existing = await service.GetAsync(new ProjectRootId("work"), CancellationToken.None);
        var missing = await service.GetAsync(new ProjectRootId("missing"), CancellationToken.None);

        Assert.True(existing.IsSuccess);
        Assert.False(missing.IsSuccess);
        Assert.Equal("project_root_not_found", missing.Error?.Code);
    }
}
