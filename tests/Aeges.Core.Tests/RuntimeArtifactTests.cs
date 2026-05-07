using Aeges.Core;

namespace Aeges.Core.Tests;

public sealed class RuntimeArtifactTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 05, 07, 12, 00, 00, TimeSpan.Zero);
    private const string Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void Create_records_artifact_metadata()
    {
        var artifact = new RuntimeArtifact(
            new ArtifactId("artifact-001"),
            new TaskId("task-001"),
            new IterationId("iteration-001"),
            ArtifactType.StdoutLog,
            "task-001/stdout.log",
            CreatedAt,
            123,
            Sha256.ToUpperInvariant());

        Assert.Equal(new ArtifactId("artifact-001"), artifact.Id);
        Assert.Equal(new TaskId("task-001"), artifact.TaskId);
        Assert.Equal(new IterationId("iteration-001"), artifact.IterationId);
        Assert.Equal(ArtifactType.StdoutLog, artifact.Type);
        Assert.Equal("task-001/stdout.log", artifact.RelativePath);
        Assert.Equal(123, artifact.SizeBytes);
        Assert.Equal(Sha256, artifact.Sha256);
        Assert.Equal(CreatedAt, artifact.CreatedAt);
    }

    [Theory]
    [InlineData("/absolute/path.log")]
    [InlineData("C:\\absolute\\path.log")]
    [InlineData("\\\\server\\share\\path.log")]
    [InlineData("../outside/path.log")]
    [InlineData("task-001/../path.log")]
    public void Create_rejects_unsafe_relative_path(string relativePath)
    {
        Assert.Throws<ArgumentException>(
            () => new RuntimeArtifact(
                new ArtifactId("artifact-001"),
                new TaskId("task-001"),
                null,
                ArtifactType.Metadata,
                relativePath,
                CreatedAt));
    }

    [Fact]
    public void Create_rejects_negative_size()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RuntimeArtifact(
                new ArtifactId("artifact-001"),
                new TaskId("task-001"),
                null,
                ArtifactType.Metadata,
                "task-001/metadata.json",
                CreatedAt,
                -1));
    }

    [Fact]
    public void Create_rejects_invalid_checksum()
    {
        Assert.Throws<ArgumentException>(
            () => new RuntimeArtifact(
                new ArtifactId("artifact-001"),
                new TaskId("task-001"),
                null,
                ArtifactType.Metadata,
                "task-001/metadata.json",
                CreatedAt,
                sha256: "not-a-checksum"));
    }

    [Theory]
    [InlineData(ArtifactType.Prompt, "prompt")]
    [InlineData(ArtifactType.Plan, "plan")]
    [InlineData(ArtifactType.StdoutLog, "stdout_log")]
    [InlineData(ArtifactType.StderrLog, "stderr_log")]
    [InlineData(ArtifactType.Result, "result")]
    [InlineData(ArtifactType.Diff, "diff")]
    [InlineData(ArtifactType.Review, "review")]
    [InlineData(ArtifactType.ApprovalRequest, "approval_request")]
    [InlineData(ArtifactType.TestOutput, "test_output")]
    [InlineData(ArtifactType.Metadata, "metadata")]
    public void Type_round_trips_storage_values(ArtifactType type, string storageValue)
    {
        Assert.Equal(storageValue, type.ToStorageValue());
        Assert.Equal(type, ArtifactTypeExtensions.FromStorageValue(storageValue));
    }
}
