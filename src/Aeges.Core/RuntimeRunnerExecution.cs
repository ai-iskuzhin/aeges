namespace Aeges.Core;

/// <summary>
/// Represents durable metadata for one worker process launched by the runtime.
/// </summary>
public sealed class RuntimeRunnerExecution
{
    private RuntimeRunnerExecution(
        RunnerExecutionId id,
        TaskId taskId,
        IterationId iterationId,
        RunnerId runnerId,
        string command,
        string workingDirectory,
        DateTimeOffset startedAt)
    {
        Id = id;
        TaskId = taskId;
        IterationId = iterationId;
        RunnerId = runnerId;
        Command = RequireText(command, nameof(command));
        WorkingDirectory = RequireText(workingDirectory, nameof(workingDirectory));
        StartedAt = startedAt;
    }

    /// <summary>
    /// Gets the runner execution identifier.
    /// </summary>
    public RunnerExecutionId Id { get; }

    /// <summary>
    /// Gets the task that owns the execution.
    /// </summary>
    public TaskId TaskId { get; }

    /// <summary>
    /// Gets the task iteration that owns the execution.
    /// </summary>
    public IterationId IterationId { get; }

    /// <summary>
    /// Gets the runner implementation that was launched.
    /// </summary>
    public RunnerId RunnerId { get; }

    /// <summary>
    /// Gets the command description used to launch the worker.
    /// </summary>
    public string Command { get; }

    /// <summary>
    /// Gets the working directory where the worker was launched.
    /// </summary>
    public string WorkingDirectory { get; }

    /// <summary>
    /// Gets the process exit code, when the worker process exited.
    /// </summary>
    public int? ExitCode { get; private set; }

    /// <summary>
    /// Gets the timestamp when the worker process started.
    /// </summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>
    /// Gets the timestamp when the worker process stopped or was interrupted.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the runtime stopped the worker because it exceeded its timeout.
    /// </summary>
    public bool TimedOut { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the runtime stopped the worker because cancellation was requested.
    /// </summary>
    public bool Cancelled { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this execution has reached a terminal process state.
    /// </summary>
    public bool IsCompleted => CompletedAt is not null;

    /// <summary>
    /// Starts tracking a runner process execution.
    /// </summary>
    /// <param name="id">The runner execution identifier.</param>
    /// <param name="taskId">The owning task identifier.</param>
    /// <param name="iterationId">The owning iteration identifier.</param>
    /// <param name="runnerId">The launched runner identifier.</param>
    /// <param name="command">The command description used to launch the runner.</param>
    /// <param name="workingDirectory">The runner working directory.</param>
    /// <param name="startedAt">The execution start timestamp.</param>
    /// <returns>A started runner execution record.</returns>
    public static RuntimeRunnerExecution Start(
        RunnerExecutionId id,
        TaskId taskId,
        IterationId iterationId,
        RunnerId runnerId,
        string command,
        string workingDirectory,
        DateTimeOffset startedAt) =>
        new(id, taskId, iterationId, runnerId, command, workingDirectory, startedAt);

    /// <summary>
    /// Rehydrates a runner execution from durable storage.
    /// </summary>
    /// <param name="id">The runner execution identifier.</param>
    /// <param name="taskId">The owning task identifier.</param>
    /// <param name="iterationId">The owning iteration identifier.</param>
    /// <param name="runnerId">The launched runner identifier.</param>
    /// <param name="command">The command description used to launch the runner.</param>
    /// <param name="workingDirectory">The runner working directory.</param>
    /// <param name="exitCode">The process exit code, when known.</param>
    /// <param name="startedAt">The execution start timestamp.</param>
    /// <param name="completedAt">The execution completion timestamp, when known.</param>
    /// <param name="timedOut">A value indicating whether the execution timed out.</param>
    /// <param name="cancelled">A value indicating whether the execution was cancelled.</param>
    /// <returns>A rehydrated runner execution record.</returns>
    public static RuntimeRunnerExecution Rehydrate(
        RunnerExecutionId id,
        TaskId taskId,
        IterationId iterationId,
        RunnerId runnerId,
        string command,
        string workingDirectory,
        int? exitCode,
        DateTimeOffset startedAt,
        DateTimeOffset? completedAt,
        bool timedOut,
        bool cancelled)
    {
        if (timedOut && cancelled)
        {
            throw new ArgumentException("A runner execution cannot be both timed out and cancelled.");
        }

        return new RuntimeRunnerExecution(id, taskId, iterationId, runnerId, command, workingDirectory, startedAt)
        {
            ExitCode = exitCode,
            CompletedAt = completedAt,
            TimedOut = timedOut,
            Cancelled = cancelled,
        };
    }

    /// <summary>
    /// Records that the worker process exited with an exit code.
    /// </summary>
    /// <param name="exitCode">The process exit code.</param>
    /// <param name="completedAt">The completion timestamp.</param>
    public void RecordExit(int exitCode, DateTimeOffset completedAt)
    {
        EnsureOpen();

        ExitCode = exitCode;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// Records that the runtime stopped the worker because it exceeded its timeout.
    /// </summary>
    /// <param name="completedAt">The completion timestamp.</param>
    public void RecordTimeout(DateTimeOffset completedAt)
    {
        EnsureOpen();

        TimedOut = true;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// Records that the runtime stopped the worker because cancellation was requested.
    /// </summary>
    /// <param name="completedAt">The completion timestamp.</param>
    public void RecordCancellation(DateTimeOffset completedAt)
    {
        EnsureOpen();

        Cancelled = true;
        CompletedAt = completedAt;
    }

    private void EnsureOpen()
    {
        if (IsCompleted)
        {
            throw new AegesDomainException($"Runner execution '{Id}' has already completed.");
        }
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value;
    }
}
