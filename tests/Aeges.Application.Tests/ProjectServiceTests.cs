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
}
