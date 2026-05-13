using Aeges.Runners;

namespace Aeges.Runners.Codex;

/// <summary>
/// Executes a built Codex CLI command and captures process output artifacts.
/// </summary>
public interface ICodexCommandExecutor
{
    /// <summary>
    /// Executes a Codex CLI command.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="stdoutPath">The path where stdout should be captured.</param>
    /// <param name="stderrPath">The path where stderr should be captured.</param>
    /// <param name="cancellationToken">A token that cancels execution.</param>
    /// <returns>The process-level execution result.</returns>
    Task<CodexCommandExecutionResult> ExecuteAsync(
        CodexRunnerCommand command,
        string stdoutPath,
        string stderrPath,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes a Codex CLI command and optionally reports parsed progress events.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="stdoutPath">The path where stdout should be captured.</param>
    /// <param name="stderrPath">The path where stderr should be captured.</param>
    /// <param name="progressSink">The optional progress sink.</param>
    /// <param name="cancellationToken">A token that cancels execution.</param>
    /// <returns>The process-level execution result.</returns>
    Task<CodexCommandExecutionResult> ExecuteAsync(
        CodexRunnerCommand command,
        string stdoutPath,
        string stderrPath,
        IRunnerProgressSink? progressSink,
        CancellationToken cancellationToken) =>
        ExecuteAsync(command, stdoutPath, stderrPath, cancellationToken);
}
