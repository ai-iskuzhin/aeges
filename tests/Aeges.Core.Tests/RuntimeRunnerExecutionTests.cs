using Aeges.Core;

namespace Aeges.Core.Tests;

public sealed class RuntimeRunnerExecutionTests
{
    [Fact]
    public void Start_creates_open_runner_execution()
    {
        var startedAt = new DateTimeOffset(2026, 05, 08, 10, 00, 00, TimeSpan.Zero);

        var execution = RuntimeRunnerExecution.Start(
            new RunnerExecutionId("runner-execution-001"),
            new TaskId("task-001"),
            new IterationId("iteration-001"),
            new RunnerId("codex"),
            "codex exec --json prompt.md",
            "/work/aeges",
            startedAt);

        Assert.Equal(new RunnerExecutionId("runner-execution-001"), execution.Id);
        Assert.Equal(new TaskId("task-001"), execution.TaskId);
        Assert.Equal(new IterationId("iteration-001"), execution.IterationId);
        Assert.Equal(new RunnerId("codex"), execution.RunnerId);
        Assert.Equal("codex exec --json prompt.md", execution.Command);
        Assert.Equal("/work/aeges", execution.WorkingDirectory);
        Assert.Equal(startedAt, execution.StartedAt);
        Assert.False(execution.IsCompleted);
        Assert.False(execution.TimedOut);
        Assert.False(execution.Cancelled);
    }

    [Fact]
    public void RecordExit_marks_execution_completed_with_exit_code()
    {
        var execution = CreateExecution();
        var completedAt = SqliteLikeClock.AddMinutes(2);

        execution.RecordExit(7, completedAt);

        Assert.True(execution.IsCompleted);
        Assert.Equal(7, execution.ExitCode);
        Assert.Equal(completedAt, execution.CompletedAt);
        Assert.False(execution.TimedOut);
        Assert.False(execution.Cancelled);
    }

    [Fact]
    public void RecordTimeout_marks_execution_timed_out()
    {
        var execution = CreateExecution();
        var completedAt = SqliteLikeClock.AddMinutes(30);

        execution.RecordTimeout(completedAt);

        Assert.True(execution.IsCompleted);
        Assert.Null(execution.ExitCode);
        Assert.Equal(completedAt, execution.CompletedAt);
        Assert.True(execution.TimedOut);
        Assert.False(execution.Cancelled);
    }

    [Fact]
    public void RecordCancellation_marks_execution_cancelled()
    {
        var execution = CreateExecution();
        var completedAt = SqliteLikeClock.AddMinutes(1);

        execution.RecordCancellation(completedAt);

        Assert.True(execution.IsCompleted);
        Assert.Null(execution.ExitCode);
        Assert.Equal(completedAt, execution.CompletedAt);
        Assert.False(execution.TimedOut);
        Assert.True(execution.Cancelled);
    }

    [Fact]
    public void Completion_cannot_be_recorded_twice()
    {
        var execution = CreateExecution();
        execution.RecordExit(0, SqliteLikeClock.AddMinutes(1));

        Assert.Throws<AegesDomainException>(() => execution.RecordTimeout(SqliteLikeClock.AddMinutes(2)));
    }

    [Fact]
    public void Rehydrate_rejects_conflicting_terminal_flags()
    {
        Assert.Throws<ArgumentException>(
            () => RuntimeRunnerExecution.Rehydrate(
                new RunnerExecutionId("runner-execution-001"),
                new TaskId("task-001"),
                new IterationId("iteration-001"),
                new RunnerId("codex"),
                "codex exec --json prompt.md",
                "/work/aeges",
                null,
                SqliteLikeClock,
                SqliteLikeClock.AddMinutes(1),
                timedOut: true,
                cancelled: true));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Start_rejects_empty_command(string command)
    {
        Assert.Throws<ArgumentException>(
            () => RuntimeRunnerExecution.Start(
                new RunnerExecutionId("runner-execution-001"),
                new TaskId("task-001"),
                new IterationId("iteration-001"),
                new RunnerId("codex"),
                command,
                "/work/aeges",
                SqliteLikeClock));
    }

    private static readonly DateTimeOffset SqliteLikeClock = new(2026, 05, 08, 10, 00, 00, TimeSpan.Zero);

    private static RuntimeRunnerExecution CreateExecution() =>
        RuntimeRunnerExecution.Start(
            new RunnerExecutionId("runner-execution-001"),
            new TaskId("task-001"),
            new IterationId("iteration-001"),
            new RunnerId("codex"),
            "codex exec --json prompt.md",
            "/work/aeges",
            SqliteLikeClock);
}
