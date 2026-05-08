namespace Aeges.Runners.Codex;

/// <summary>
/// Describes the process-level outcome of a Codex CLI command.
/// </summary>
/// <param name="ExitCode">The process exit code, when the process exited normally.</param>
/// <param name="TimedOut">Whether the command exceeded its configured timeout.</param>
/// <param name="Cancelled">Whether the command was cancelled by the caller.</param>
/// <param name="ErrorSummary">A short process execution error summary, when available.</param>
public sealed record CodexCommandExecutionResult(
    int? ExitCode,
    bool TimedOut,
    bool Cancelled,
    string? ErrorSummary = null)
{
    /// <summary>
    /// Creates a normally exited command result.
    /// </summary>
    /// <param name="exitCode">The process exit code.</param>
    /// <returns>A command result.</returns>
    public static CodexCommandExecutionResult Exited(int exitCode) =>
        new(exitCode, TimedOut: false, Cancelled: false);

    /// <summary>
    /// Creates a timed-out command result.
    /// </summary>
    /// <param name="errorSummary">A short timeout summary.</param>
    /// <returns>A command result.</returns>
    public static CodexCommandExecutionResult Timeout(string errorSummary) =>
        new(ExitCode: null, TimedOut: true, Cancelled: false, ErrorSummary: errorSummary);

    /// <summary>
    /// Creates a cancelled command result.
    /// </summary>
    /// <param name="errorSummary">A short cancellation summary.</param>
    /// <returns>A command result.</returns>
    public static CodexCommandExecutionResult Cancellation(string errorSummary) =>
        new(ExitCode: null, TimedOut: false, Cancelled: true, ErrorSummary: errorSummary);
}
