using Aeges.Application.Projects;
using Aeges.Core;

namespace Aeges.Application.Tests;

public sealed class ProjectServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 07, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task RegisterAsync_creates_project_and_saves_changes()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var service = new ProjectService(unitOfWork, new FixedClock(Now));

        var result = await service.RegisterAsync(
            new RegisterProjectRequest("Aeges", "/work/aeges", new ProjectId("project-001")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(new ProjectId("project-001"), result.Value.Id);
        Assert.Equal("Aeges", result.Value.Name);
        Assert.Equal("/work/aeges", result.Value.Path);
        Assert.Equal(Now, result.Value.CreatedAt);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ListAsync_returns_registered_projects()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var service = new ProjectService(unitOfWork, new FixedClock(Now));
        await service.RegisterAsync(new RegisterProjectRequest("Aeges", "/work/aeges", new ProjectId("project-001")), CancellationToken.None);

        var projects = await service.ListAsync(CancellationToken.None);

        Assert.Single(projects);
        Assert.Equal(new ProjectId("project-001"), projects[0].Id);
    }

    [Fact]
    public async Task ListActiveAsync_excludes_archived_projects()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        var service = new ProjectService(unitOfWork, clock);
        await service.RegisterAsync(new RegisterProjectRequest("Aeges", "/work/aeges", new ProjectId("project-001")), CancellationToken.None);
        await service.RegisterAsync(new RegisterProjectRequest("Archive", "/work/archive", new ProjectId("project-002")), CancellationToken.None);

        clock.Now = Now.AddMinutes(1);
        await service.ArchiveAsync(new ProjectId("project-002"), CancellationToken.None);

        var projects = await service.ListActiveAsync(CancellationToken.None);

        Assert.Single(projects);
        Assert.Equal(new ProjectId("project-001"), projects[0].Id);
    }

    [Fact]
    public async Task GetAsync_returns_project_or_failure()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var service = new ProjectService(unitOfWork, new FixedClock(Now));
        await service.RegisterAsync(new RegisterProjectRequest("Aeges", "/work/aeges", new ProjectId("project-001")), CancellationToken.None);

        var existing = await service.GetAsync(new ProjectId("project-001"), CancellationToken.None);
        var missing = await service.GetAsync(new ProjectId("missing"), CancellationToken.None);

        Assert.True(existing.IsSuccess);
        Assert.False(missing.IsSuccess);
        Assert.Equal("project_not_found", missing.Error?.Code);
    }

    [Fact]
    public async Task ArchiveAsync_archives_project_and_saves_changes()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        var service = new ProjectService(unitOfWork, clock);
        await service.RegisterAsync(new RegisterProjectRequest("Aeges", "/work/aeges", new ProjectId("project-001")), CancellationToken.None);
        clock.Now = Now.AddMinutes(5);

        var result = await service.ArchiveAsync(new ProjectId("project-001"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsArchived);
        Assert.Equal(Now.AddMinutes(5), result.Value.ArchivedAt);
        Assert.Equal(2, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ArchiveAsync_returns_failure_when_project_is_missing()
    {
        var service = new ProjectService(new InMemoryUnitOfWork(), new FixedClock(Now));

        var result = await service.ArchiveAsync(new ProjectId("missing"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("project_not_found", result.Error?.Code);
    }
}
