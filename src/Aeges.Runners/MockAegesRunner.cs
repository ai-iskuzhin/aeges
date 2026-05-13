using Aeges.Core;

namespace Aeges.Runners;

/// <summary>
/// Provides a deterministic runner implementation for tests and local dry runs.
/// </summary>
public sealed class MockAegesRunner : IAegesRunner
{
    private readonly MockRunnerOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockAegesRunner"/> class.
    /// </summary>
    /// <param name="options">The mock runner options.</param>
    public MockAegesRunner(MockRunnerOptions? options = null)
    {
        this.options = Validate(options ?? new MockRunnerOptions());
        Id = this.options.RunnerId ?? new RunnerId("mock");
    }

    /// <inheritdoc />
    public RunnerId Id { get; }

    /// <summary>
    /// Gets the most recent request observed by the mock runner.
    /// </summary>
    public RunnerRequest? LastRequest { get; private set; }

    /// <summary>
    /// Gets the number of times the mock runner was invoked.
    /// </summary>
    public int InvocationCount { get; private set; }

    /// <inheritdoc />
    public async Task<RunnerResult> RunAsync(RunnerRequest request, CancellationToken cancellationToken)
        => await RunAsync(request, null, cancellationToken);

    /// <inheritdoc />
    public async Task<RunnerResult> RunAsync(
        RunnerRequest request,
        IRunnerProgressSink? progressSink,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        InvocationCount++;

        await ReportProgressAsync(progressSink, "runner.started", "Mock runner started.", cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            return RunnerResult.Cancelled("Mock runner was cancelled before execution.");
        }

        if (options.ExecutionDelay is { } executionDelay && executionDelay > TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(executionDelay, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return RunnerResult.Cancelled("Mock runner was cancelled during execution.");
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return RunnerResult.Cancelled("Mock runner was cancelled after execution delay.");
        }

        if (options.Behavior == MockRunnerBehavior.Succeed)
        {
            Directory.CreateDirectory(request.ArtifactOutputDirectory);
            var stdoutPath = Path.Combine(request.ArtifactOutputDirectory, "stdout.log");
            var stderrPath = Path.Combine(request.ArtifactOutputDirectory, "stderr.log");
            var resultPath = Path.Combine(request.ArtifactOutputDirectory, "result.md");

            await File.WriteAllTextAsync(stdoutPath, "Mock runner completed successfully.\n", cancellationToken);
            await File.WriteAllTextAsync(stderrPath, string.Empty, cancellationToken);
            await File.WriteAllTextAsync(resultPath, "Mock runner result: success.\n", cancellationToken);
            await ReportProgressAsync(progressSink, "runner.message", "Mock runner completed successfully.", cancellationToken);

            return RunnerResult.Succeeded(
                stdoutPath: stdoutPath,
                stderrPath: stderrPath,
                resultArtifactPath: resultPath,
                producedArtifactPaths:
                [
                    stdoutPath,
                    stderrPath,
                    resultPath,
                ]);
        }

        if (options.Behavior == MockRunnerBehavior.Fail)
        {
            return RunnerResult.Failed(
                await ReportAndReturnAsync(
                    progressSink,
                    "runner.failed",
                    options.ErrorSummary ?? "Mock runner failed.",
                    cancellationToken),
                exitCode: 1);
        }

        if (options.Behavior == MockRunnerBehavior.TimeOut)
        {
            return RunnerResult.TimedOut(
                await ReportAndReturnAsync(
                    progressSink,
                    "runner.timed_out",
                    options.ErrorSummary ?? "Mock runner timed out.",
                    cancellationToken));
        }

        if (options.Behavior == MockRunnerBehavior.RequireApproval)
        {
            return RunnerResult.ApprovalRequired(
                await ReportAndReturnAsync(
                    progressSink,
                    "runner.approval_required",
                    options.ErrorSummary ?? "Mock runner requires approval.",
                    cancellationToken));
        }

        throw new ArgumentOutOfRangeException(nameof(options), options.Behavior, "Unknown mock runner behavior.");
    }

    private static async Task<string> ReportAndReturnAsync(
        IRunnerProgressSink? progressSink,
        string eventType,
        string message,
        CancellationToken cancellationToken)
    {
        await ReportProgressAsync(progressSink, eventType, message, cancellationToken);

        return message;
    }

    private static async Task ReportProgressAsync(
        IRunnerProgressSink? progressSink,
        string eventType,
        string message,
        CancellationToken cancellationToken)
    {
        if (progressSink is not null)
        {
            await progressSink.ReportAsync(new RunnerProgressEvent(eventType, message), cancellationToken);
        }
    }

    private static MockRunnerOptions Validate(MockRunnerOptions options)
    {
        if (options.ExecutionDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.ExecutionDelay, "Execution delay must not be negative.");
        }

        if (options.ErrorSummary is not null && string.IsNullOrWhiteSpace(options.ErrorSummary))
        {
            throw new ArgumentException("Error summary must not be empty.", nameof(options));
        }

        return options;
    }
}
