using Aeges.Core;

namespace Aeges.Core.Tests;

public sealed class RuntimeTaskTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 05, 07, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Create_initializes_queued_task()
    {
        var task = CreateTask();

        Assert.Equal(RuntimeTaskStatus.Queued, task.Status);
        Assert.Equal(0, task.CurrentIteration);
        Assert.Equal(3, task.MaxIterations);
        Assert.Equal(CreatedAt, task.CreatedAt);
        Assert.Equal(CreatedAt, task.UpdatedAt);
        Assert.Null(task.StartedAt);
        Assert.Null(task.CompletedAt);
        Assert.Null(task.CancelledAt);
        Assert.Null(task.FailureReason);
    }

    [Fact]
    public void Rehydrate_restores_task_state()
    {
        var updatedAt = CreatedAt.AddMinutes(6);
        var startedAt = CreatedAt.AddMinutes(1);
        var completedAt = CreatedAt.AddMinutes(5);

        var task = RuntimeTask.Rehydrate(
            new TaskId("task-001"),
            new ProjectId("project-001"),
            new MachineId("machine-001"),
            "Implement durable task lifecycle",
            "Create the initial task model and lifecycle transitions.",
            RuntimeTaskStatus.Completed,
            priority: 10,
            maxIterations: 3,
            currentIteration: 2,
            CreatedAt,
            updatedAt,
            startedAt,
            completedAt,
            cancelledAt: null,
            failureReason: null);

        Assert.Equal(RuntimeTaskStatus.Completed, task.Status);
        Assert.Equal(10, task.Priority);
        Assert.Equal(3, task.MaxIterations);
        Assert.Equal(2, task.CurrentIteration);
        Assert.Equal(CreatedAt, task.CreatedAt);
        Assert.Equal(updatedAt, task.UpdatedAt);
        Assert.Equal(startedAt, task.StartedAt);
        Assert.Equal(completedAt, task.CompletedAt);
        Assert.Null(task.CancelledAt);
        Assert.Null(task.FailureReason);
    }

    [Fact]
    public void Rehydrate_rejects_invalid_current_iteration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RuntimeTask.Rehydrate(
                new TaskId("task-001"),
                new ProjectId("project-001"),
                new MachineId("machine-001"),
                "Implement durable task lifecycle",
                "Create the initial task model and lifecycle transitions.",
                RuntimeTaskStatus.Running,
                priority: 0,
                maxIterations: 3,
                currentIteration: 4,
                CreatedAt,
                CreatedAt,
                startedAt: null,
                completedAt: null,
                cancelledAt: null,
                failureReason: null));
    }

    [Fact]
    public void Task_can_follow_primary_lifecycle()
    {
        var task = CreateTask();
        var planningAt = CreatedAt.AddMinutes(1);
        var runningAt = CreatedAt.AddMinutes(2);
        var reviewingAt = CreatedAt.AddMinutes(3);
        var completedAt = CreatedAt.AddMinutes(4);

        task.StartPlanning(planningAt);
        task.StartRunning(runningAt);
        task.StartReview(reviewingAt);
        task.Complete(completedAt);

        Assert.Equal(RuntimeTaskStatus.Completed, task.Status);
        Assert.Equal(planningAt, task.StartedAt);
        Assert.Equal(completedAt, task.CompletedAt);
        Assert.Equal(completedAt, task.UpdatedAt);
        Assert.True(task.Status.IsTerminal());
    }

    [Fact]
    public void Task_rejects_invalid_transition()
    {
        var task = CreateTask();

        var exception = Assert.Throws<InvalidTaskStatusTransitionException>(
            () => task.Complete(CreatedAt.AddMinutes(1)));

        Assert.Equal(RuntimeTaskStatus.Queued, exception.From);
        Assert.Equal(RuntimeTaskStatus.Completed, exception.To);
        Assert.Equal(RuntimeTaskStatus.Queued, task.Status);
    }

    [Fact]
    public void Terminal_task_rejects_further_transitions()
    {
        var task = CreateTask();

        task.StartPlanning(CreatedAt.AddMinutes(1));
        task.Cancel(CreatedAt.AddMinutes(2));

        Assert.Throws<InvalidTaskStatusTransitionException>(
            () => task.StartRunning(CreatedAt.AddMinutes(3)));
        Assert.Equal(RuntimeTaskStatus.Cancelled, task.Status);
    }

    [Fact]
    public void AdvanceIteration_enforces_max_iterations()
    {
        var task = CreateTask(maxIterations: 2);

        task.AdvanceIteration(CreatedAt.AddMinutes(1));
        task.AdvanceIteration(CreatedAt.AddMinutes(2));

        Assert.Equal(2, task.CurrentIteration);
        Assert.Throws<AegesDomainException>(() => task.AdvanceIteration(CreatedAt.AddMinutes(3)));
    }

    [Fact]
    public void Reviewing_task_can_be_requeued_for_revision()
    {
        var task = CreateTask(maxIterations: 2);

        task.StartPlanning(CreatedAt.AddMinutes(1));
        task.AdvanceIteration(CreatedAt.AddMinutes(2));
        task.StartRunning(CreatedAt.AddMinutes(3));
        task.StartReview(CreatedAt.AddMinutes(4));
        task.RequeueForRevision(CreatedAt.AddMinutes(5));

        Assert.Equal(RuntimeTaskStatus.Queued, task.Status);
        Assert.Equal(1, task.CurrentIteration);
        Assert.Equal(CreatedAt.AddMinutes(5), task.UpdatedAt);
    }

    [Fact]
    public void RequeueForRevision_enforces_iteration_limit()
    {
        var task = CreateTask(maxIterations: 1);

        task.StartPlanning(CreatedAt.AddMinutes(1));
        task.AdvanceIteration(CreatedAt.AddMinutes(2));
        task.StartRunning(CreatedAt.AddMinutes(3));
        task.StartReview(CreatedAt.AddMinutes(4));

        Assert.Throws<AegesDomainException>(() => task.RequeueForRevision(CreatedAt.AddMinutes(5)));
        Assert.Equal(RuntimeTaskStatus.Reviewing, task.Status);
    }

    [Theory]
    [InlineData(RuntimeTaskStatus.Queued, "queued")]
    [InlineData(RuntimeTaskStatus.Planning, "planning")]
    [InlineData(RuntimeTaskStatus.Running, "running")]
    [InlineData(RuntimeTaskStatus.Reviewing, "reviewing")]
    [InlineData(RuntimeTaskStatus.WaitingApproval, "waiting_approval")]
    [InlineData(RuntimeTaskStatus.Completed, "completed")]
    [InlineData(RuntimeTaskStatus.Failed, "failed")]
    [InlineData(RuntimeTaskStatus.Cancelled, "cancelled")]
    public void Status_round_trips_storage_values(RuntimeTaskStatus status, string storageValue)
    {
        Assert.Equal(storageValue, status.ToStorageValue());
        Assert.Equal(status, RuntimeTaskStatusExtensions.FromStorageValue(storageValue));
    }

    private static RuntimeTask CreateTask(int maxIterations = 3) =>
        RuntimeTask.Create(
            new TaskId("task-001"),
            new ProjectId("project-001"),
            new MachineId("machine-001"),
            "Implement durable task lifecycle",
            "Create the initial task model and lifecycle transitions.",
            CreatedAt,
            maxIterations: maxIterations);
}
