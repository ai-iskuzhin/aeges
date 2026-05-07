using Aeges.Core;
using Aeges.Storage.Sqlite.Repositories;

namespace Aeges.Storage.Sqlite.Tests;

public sealed class SqliteArtifactRepositoryTests
{
    [Fact]
    public async Task Add_and_get_round_trips_artifact()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SeedTaskAndIterationAsync(context);
        var repository = new SqliteArtifactRepository(context);
        var artifact = CreateArtifact(new ArtifactId("artifact-001"), ArtifactType.Prompt, "task-001/prompt.md");

        await repository.AddAsync(artifact, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(new ArtifactId("artifact-001"), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(artifact.Id, stored.Id);
        Assert.Equal(artifact.TaskId, stored.TaskId);
        Assert.Equal(artifact.IterationId, stored.IterationId);
        Assert.Equal(artifact.Type, stored.Type);
        Assert.Equal(artifact.RelativePath, stored.RelativePath);
        Assert.Equal(artifact.SizeBytes, stored.SizeBytes);
        Assert.Equal(artifact.Sha256, stored.Sha256);
        Assert.Equal(artifact.CreatedAt, stored.CreatedAt);
    }

    [Fact]
    public async Task List_methods_return_artifacts_ordered_by_creation()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SeedTaskAndIterationAsync(context);
        var repository = new SqliteArtifactRepository(context);

        await repository.AddAsync(
            CreateArtifact(new ArtifactId("artifact-002"), ArtifactType.Result, "task-001/result.md", SqliteRepositorySeed.CreatedAt.AddMinutes(2)),
            CancellationToken.None);
        await repository.AddAsync(
            CreateArtifact(new ArtifactId("artifact-001"), ArtifactType.Prompt, "task-001/prompt.md", SqliteRepositorySeed.CreatedAt.AddMinutes(1)),
            CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var byTask = await repository.ListByTaskAsync(new TaskId("task-001"), CancellationToken.None);
        var byIteration = await repository.ListByIterationAsync(new IterationId("iteration-001"), CancellationToken.None);

        Assert.Collection(
            byTask,
            artifact => Assert.Equal(new ArtifactId("artifact-001"), artifact.Id),
            artifact => Assert.Equal(new ArtifactId("artifact-002"), artifact.Id));
        Assert.Collection(
            byIteration,
            artifact => Assert.Equal(new ArtifactId("artifact-001"), artifact.Id),
            artifact => Assert.Equal(new ArtifactId("artifact-002"), artifact.Id));
    }

    [Fact]
    public async Task Get_returns_null_when_artifact_does_not_exist()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteArtifactRepository(context);

        var stored = await repository.GetByIdAsync(new ArtifactId("missing"), CancellationToken.None);

        Assert.Null(stored);
    }

    private static RuntimeArtifact CreateArtifact(
        ArtifactId id,
        ArtifactType type,
        string relativePath,
        DateTimeOffset? createdAt = null) =>
        new(
            id,
            new TaskId("task-001"),
            new IterationId("iteration-001"),
            type,
            relativePath,
            createdAt ?? SqliteRepositorySeed.CreatedAt,
            42,
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");

    private static async Task SeedTaskAndIterationAsync(AegesDbContext context)
    {
        await SqliteRepositorySeed.SeedProjectMachineAndTaskAsync(context);
        var iterations = new SqliteIterationRepository(context);
        await iterations.AddAsync(
            TaskIteration.Create(
                new IterationId("iteration-001"),
                new TaskId("task-001"),
                1,
                new RunnerId("codex"),
                SqliteRepositorySeed.CreatedAt),
            CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);
    }
}
