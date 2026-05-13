using Aeges.Core;
using Aeges.Runners;

namespace Aeges.Runners.Codex;

/// <summary>
/// Executes governed runner requests through the Codex CLI.
/// </summary>
public sealed class CodexRunner : IAegesRunner
{
    private readonly CodexRunnerCommandBuilder commandBuilder;
    private readonly ICodexCommandExecutor commandExecutor;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodexRunner"/> class.
    /// </summary>
    /// <param name="options">The Codex runner options.</param>
    /// <param name="commandExecutor">The command executor used to launch Codex.</param>
    /// <param name="executableResolver">The resolver used to locate the Codex executable.</param>
    public CodexRunner(
        CodexRunnerOptions? options = null,
        ICodexCommandExecutor? commandExecutor = null,
        ICodexExecutableResolver? executableResolver = null)
    {
        commandBuilder = new CodexRunnerCommandBuilder(options, executableResolver);
        this.commandExecutor = commandExecutor ?? new ProcessCodexCommandExecutor();
    }

    /// <inheritdoc />
    public RunnerId Id { get; } = new("codex");

    /// <inheritdoc />
    public async Task<RunnerResult> RunAsync(
        RunnerRequest request,
        CancellationToken cancellationToken)
        => await RunAsync(request, null, cancellationToken);

    /// <inheritdoc />
    public async Task<RunnerResult> RunAsync(
        RunnerRequest request,
        IRunnerProgressSink? progressSink,
        CancellationToken cancellationToken)
    {
        var stdoutPath = Path.Combine(request.ArtifactOutputDirectory, "codex.stdout.jsonl");
        var stderrPath = Path.Combine(request.ArtifactOutputDirectory, "codex.stderr.log");

        CodexRunnerCommand command;

        try
        {
            command = commandBuilder.Build(request);
        }
        catch (CodexRunnerUnavailableException exception)
        {
            return RunnerResult.Failed(exception.Message);
        }

        try
        {
            var execution = await commandExecutor.ExecuteAsync(command, stdoutPath, stderrPath, progressSink, cancellationToken);
            var externalSessionId = ReadExternalSessionId(stdoutPath) ?? request.ExternalSessionId;

            if (execution.Cancelled)
            {
                return RunnerResult.Cancelled(
                    execution.ErrorSummary ?? "Codex runner was cancelled.",
                    stdoutPath,
                    stderrPath,
                    externalSessionId);
            }

            if (execution.TimedOut)
            {
                return RunnerResult.TimedOut(
                    execution.ErrorSummary ?? "Codex runner exceeded its timeout.",
                    stdoutPath,
                    stderrPath,
                    externalSessionId);
            }

            if (execution.ExitCode == 0)
            {
                return RunnerResult.Succeeded(
                    exitCode: 0,
                    stdoutPath: stdoutPath,
                    stderrPath: stderrPath,
                    producedArtifactPaths: [stdoutPath, stderrPath],
                    externalSessionId: externalSessionId);
            }

            return RunnerResult.Failed(
                $"Codex exited with code {execution.ExitCode}.",
                execution.ExitCode,
                stdoutPath,
                stderrPath,
                externalSessionId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return RunnerResult.Cancelled("Codex runner was cancelled.", stdoutPath, stderrPath, request.ExternalSessionId);
        }
    }

    private static string? ReadExternalSessionId(string stdoutPath)
    {
        if (!File.Exists(stdoutPath))
        {
            return null;
        }

        foreach (var line in File.ReadLines(stdoutPath))
        {
            if (CodexRunnerJsonEvents.TryReadThreadId(line, out var threadId))
            {
                return threadId;
            }
        }

        return null;
    }
}
