namespace Aeges.Storage.Sqlite.Entities;

/// <summary>
/// EF Core persistence record for one runner process execution.
/// </summary>
internal sealed class RunnerExecutionRecord
{
    /// <summary>
    /// Gets or sets the runner execution identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the owning task identifier.
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the iteration identifier for the execution attempt.
    /// </summary>
    public string IterationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the runner implementation identifier.
    /// </summary>
    public string RunnerId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the command description used to start the runner.
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the working directory used for runner execution.
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the process exit code, when the runner has exited.
    /// </summary>
    public int? ExitCode { get; set; }

    /// <summary>
    /// Gets or sets the execution start timestamp.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the execution completion timestamp.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the execution exceeded its timeout.
    /// </summary>
    public bool TimedOut { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the execution was cancelled.
    /// </summary>
    public bool Cancelled { get; set; }

    /// <summary>
    /// Gets or sets the owning task navigation.
    /// </summary>
    public TaskRecord? Task { get; set; }

    /// <summary>
    /// Gets or sets the owning iteration navigation.
    /// </summary>
    public TaskIterationRecord? Iteration { get; set; }
}
