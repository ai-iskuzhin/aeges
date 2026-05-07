using Aeges.Core;

namespace Aeges.Core.Tests;

public sealed class TaskIterationTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 05, 07, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Create_initializes_created_iteration()
    {
        var iteration = CreateIteration();

        Assert.Equal(TaskIterationStatus.Created, iteration.Status);
        Assert.Equal(1, iteration.IterationNumber);
        Assert.Equal(CreatedAt, iteration.CreatedAt);
        Assert.Equal(CreatedAt, iteration.UpdatedAt);
        Assert.Null(iteration.StartedAt);
        Assert.Null(iteration.CompletedAt);
        Assert.Null(iteration.FailureReason);
        Assert.Null(iteration.PromptArtifactId);
        Assert.Null(iteration.ResultArtifactId);
        Assert.Null(iteration.DiffArtifactId);
    }

    [Fact]
    public void Rehydrate_restores_iteration_state()
    {
        var updatedAt = CreatedAt.AddMinutes(5);
        var startedAt = CreatedAt.AddMinutes(1);
        var completedAt = CreatedAt.AddMinutes(4);

        var iteration = TaskIteration.Rehydrate(
            new IterationId("iteration-001"),
            new TaskId("task-001"),
            2,
            TaskIterationStatus.Completed,
            new RunnerId("codex"),
            "/tmp/aeges/worktrees/project-001/task-001",
            new ArtifactId("artifact-prompt"),
            new ArtifactId("artifact-result"),
            new ArtifactId("artifact-diff"),
            CreatedAt,
            updatedAt,
            startedAt,
            completedAt,
            failureReason: null);

        Assert.Equal(TaskIterationStatus.Completed, iteration.Status);
        Assert.Equal(2, iteration.IterationNumber);
        Assert.Equal("/tmp/aeges/worktrees/project-001/task-001", iteration.WorktreePath);
        Assert.Equal(new ArtifactId("artifact-prompt"), iteration.PromptArtifactId);
        Assert.Equal(new ArtifactId("artifact-result"), iteration.ResultArtifactId);
        Assert.Equal(new ArtifactId("artifact-diff"), iteration.DiffArtifactId);
        Assert.Equal(updatedAt, iteration.UpdatedAt);
        Assert.Equal(startedAt, iteration.StartedAt);
        Assert.Equal(completedAt, iteration.CompletedAt);
        Assert.Null(iteration.FailureReason);
    }

    [Fact]
    public void Iteration_can_follow_primary_lifecycle()
    {
        var iteration = CreateIteration();
        var runningAt = CreatedAt.AddMinutes(1);
        var reviewingAt = CreatedAt.AddMinutes(2);
        var completedAt = CreatedAt.AddMinutes(3);

        iteration.StartRunning(runningAt);
        iteration.StartReview(reviewingAt);
        iteration.Complete(completedAt);

        Assert.Equal(TaskIterationStatus.Completed, iteration.Status);
        Assert.Equal(runningAt, iteration.StartedAt);
        Assert.Equal(completedAt, iteration.CompletedAt);
        Assert.Equal(completedAt, iteration.UpdatedAt);
        Assert.True(iteration.Status.IsTerminal());
    }

    [Fact]
    public void Iteration_rejects_invalid_transition()
    {
        var iteration = CreateIteration();

        var exception = Assert.Throws<InvalidTaskIterationStatusTransitionException>(
            () => iteration.Complete(CreatedAt.AddMinutes(1)));

        Assert.Equal(TaskIterationStatus.Created, exception.From);
        Assert.Equal(TaskIterationStatus.Completed, exception.To);
        Assert.Equal(TaskIterationStatus.Created, iteration.Status);
    }

    [Fact]
    public void Terminal_iteration_rejects_further_transitions()
    {
        var iteration = CreateIteration();

        iteration.Cancel(CreatedAt.AddMinutes(1));

        Assert.Throws<InvalidTaskIterationStatusTransitionException>(
            () => iteration.StartRunning(CreatedAt.AddMinutes(2)));
    }

    [Fact]
    public void Iteration_records_assigned_artifacts_and_worktree()
    {
        var iteration = CreateIteration();
        var updatedAt = CreatedAt.AddMinutes(1);

        iteration.AssignWorktree("/tmp/aeges/worktrees/project-001/task-001", updatedAt);
        iteration.AttachPromptArtifact(new ArtifactId("artifact-prompt"), updatedAt.AddMinutes(1));
        iteration.AttachResultArtifact(new ArtifactId("artifact-result"), updatedAt.AddMinutes(2));
        iteration.AttachDiffArtifact(new ArtifactId("artifact-diff"), updatedAt.AddMinutes(3));

        Assert.Equal("/tmp/aeges/worktrees/project-001/task-001", iteration.WorktreePath);
        Assert.Equal(new ArtifactId("artifact-prompt"), iteration.PromptArtifactId);
        Assert.Equal(new ArtifactId("artifact-result"), iteration.ResultArtifactId);
        Assert.Equal(new ArtifactId("artifact-diff"), iteration.DiffArtifactId);
        Assert.Equal(updatedAt.AddMinutes(3), iteration.UpdatedAt);
    }

    [Theory]
    [InlineData(TaskIterationStatus.Created, "created")]
    [InlineData(TaskIterationStatus.Running, "running")]
    [InlineData(TaskIterationStatus.Reviewing, "reviewing")]
    [InlineData(TaskIterationStatus.WaitingApproval, "waiting_approval")]
    [InlineData(TaskIterationStatus.Completed, "completed")]
    [InlineData(TaskIterationStatus.Failed, "failed")]
    [InlineData(TaskIterationStatus.Cancelled, "cancelled")]
    public void Status_round_trips_storage_values(TaskIterationStatus status, string storageValue)
    {
        Assert.Equal(storageValue, status.ToStorageValue());
        Assert.Equal(status, TaskIterationStatusExtensions.FromStorageValue(storageValue));
    }

    private static TaskIteration CreateIteration() =>
        TaskIteration.Create(
            new IterationId("iteration-001"),
            new TaskId("task-001"),
            1,
            new RunnerId("codex"),
            CreatedAt);
}
