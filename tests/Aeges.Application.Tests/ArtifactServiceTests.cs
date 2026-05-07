using Aeges.Application.Artifacts;
using Aeges.Application.Machines;
using Aeges.Application.Projects;
using Aeges.Application.Tasks;
using Aeges.Core;

namespace Aeges.Application.Tests;

public sealed class ArtifactServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 07, 12, 00, 00, TimeSpan.Zero);
    private const string Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task RegisterAsync_records_artifact_metadata()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAsync(unitOfWork, clock);
        var service = new ArtifactService(unitOfWork, clock);

        var result = await service.RegisterAsync(
            new RegisterArtifactRequest(
                new TaskId("task-001"),
                IterationId: null,
                ArtifactType.Metadata,
                "task-001/metadata.json",
                SizeBytes: 123,
                Sha256: Sha256.ToUpperInvariant(),
                ArtifactId: new ArtifactId("artifact-001")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(new ArtifactId("artifact-001"), result.Value.Id);
        Assert.Equal(new TaskId("task-001"), result.Value.TaskId);
        Assert.Null(result.Value.IterationId);
        Assert.Equal(ArtifactType.Metadata, result.Value.Type);
        Assert.Equal("task-001/metadata.json", result.Value.RelativePath);
        Assert.Equal(123, result.Value.SizeBytes);
        Assert.Equal(Sha256, result.Value.Sha256);
        Assert.Equal(Now, result.Value.CreatedAt);
    }

    [Fact]
    public async Task RegisterAsync_requires_existing_task()
    {
        var service = new ArtifactService(new InMemoryUnitOfWork(), new FixedClock(Now));

        var result = await service.RegisterAsync(
            new RegisterArtifactRequest(new TaskId("missing"), null, ArtifactType.Metadata, "task-001/metadata.json"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("task_not_found", result.Error?.Code);
    }

    [Fact]
    public async Task RegisterAsync_requires_matching_iteration_when_iteration_is_supplied()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAsync(unitOfWork, clock);
        var service = new ArtifactService(unitOfWork, clock);

        var result = await service.RegisterAsync(
            new RegisterArtifactRequest(
                new TaskId("task-001"),
                new IterationId("missing"),
                ArtifactType.Prompt,
                "task-001/prompt.md"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("iteration_not_found", result.Error?.Code);
    }

    [Fact]
    public async Task RegisterAsync_returns_failure_for_invalid_metadata()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAsync(unitOfWork, clock);
        var service = new ArtifactService(unitOfWork, clock);
        var saveCount = unitOfWork.SaveChangesCount;

        var result = await service.RegisterAsync(
            new RegisterArtifactRequest(new TaskId("task-001"), null, ArtifactType.Metadata, "../outside.json"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_artifact_metadata", result.Error?.Code);
        Assert.Equal(saveCount, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task RegisterAsync_attaches_prompt_result_and_diff_artifacts_to_iteration()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAsync(unitOfWork, clock);
        await unitOfWork.Iterations.AddAsync(
            TaskIteration.Create(new IterationId("iteration-001"), new TaskId("task-001"), 1, new RunnerId("runner-codex"), Now),
            CancellationToken.None);
        var service = new ArtifactService(unitOfWork, clock);

        await service.RegisterAsync(
            new RegisterArtifactRequest(
                new TaskId("task-001"),
                new IterationId("iteration-001"),
                ArtifactType.Prompt,
                "task-001/iteration-001/prompt.md",
                ArtifactId: new ArtifactId("artifact-prompt")),
            CancellationToken.None);
        await service.RegisterAsync(
            new RegisterArtifactRequest(
                new TaskId("task-001"),
                new IterationId("iteration-001"),
                ArtifactType.Result,
                "task-001/iteration-001/result.md",
                ArtifactId: new ArtifactId("artifact-result")),
            CancellationToken.None);
        await service.RegisterAsync(
            new RegisterArtifactRequest(
                new TaskId("task-001"),
                new IterationId("iteration-001"),
                ArtifactType.Diff,
                "task-001/iteration-001/diff.patch",
                ArtifactId: new ArtifactId("artifact-diff")),
            CancellationToken.None);

        var iteration = await unitOfWork.Iterations.GetByIdAsync(new IterationId("iteration-001"), CancellationToken.None);

        Assert.NotNull(iteration);
        Assert.Equal(new ArtifactId("artifact-prompt"), iteration.PromptArtifactId);
        Assert.Equal(new ArtifactId("artifact-result"), iteration.ResultArtifactId);
        Assert.Equal(new ArtifactId("artifact-diff"), iteration.DiffArtifactId);
    }

    [Fact]
    public async Task List_methods_return_registered_artifacts()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var clock = new FixedClock(Now);
        await SeedTaskAsync(unitOfWork, clock);
        await unitOfWork.Iterations.AddAsync(
            TaskIteration.Create(new IterationId("iteration-001"), new TaskId("task-001"), 1, new RunnerId("runner-codex"), Now),
            CancellationToken.None);
        var service = new ArtifactService(unitOfWork, clock);
        await service.RegisterAsync(
            new RegisterArtifactRequest(
                new TaskId("task-001"),
                new IterationId("iteration-001"),
                ArtifactType.StdoutLog,
                "task-001/iteration-001/stdout.log",
                ArtifactId: new ArtifactId("artifact-001")),
            CancellationToken.None);

        var byTask = await service.ListByTaskAsync(new TaskId("task-001"), CancellationToken.None);
        var byIteration = await service.ListByIterationAsync(new IterationId("iteration-001"), CancellationToken.None);
        var loaded = await service.GetAsync(new ArtifactId("artifact-001"), CancellationToken.None);

        Assert.Single(byTask);
        Assert.Single(byIteration);
        Assert.True(loaded.IsSuccess);
        Assert.Equal(new ArtifactId("artifact-001"), loaded.Value?.Id);
    }

    [Fact]
    public async Task GetAsync_returns_failure_when_artifact_is_missing()
    {
        var service = new ArtifactService(new InMemoryUnitOfWork(), new FixedClock(Now));

        var result = await service.GetAsync(new ArtifactId("missing"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("artifact_not_found", result.Error?.Code);
    }

    private static async Task SeedTaskAsync(InMemoryUnitOfWork unitOfWork, FixedClock clock)
    {
        await new ProjectService(unitOfWork, clock).RegisterAsync(
            new RegisterProjectRequest("Aeges", "/work/aeges", new ProjectId("project-001")),
            CancellationToken.None);
        await new MachineService(unitOfWork, clock).RegisterAsync(
            new RegisterMachineRequest("home-laptop", "macOS arm64", new MachineId("machine-001")),
            CancellationToken.None);
        await new TaskService(unitOfWork, clock).CreateAsync(
            new CreateTaskRequest(
                new ProjectId("project-001"),
                new MachineId("machine-001"),
                "Build CLI",
                "Create commands.",
                TaskId: new TaskId("task-001")),
            CancellationToken.None);
    }
}
