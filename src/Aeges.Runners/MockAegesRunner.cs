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
    {
        LastRequest = request;
        InvocationCount++;

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

        return options.Behavior switch
        {
            MockRunnerBehavior.Succeed => RunnerResult.Succeeded(
                stdoutPath: Path.Combine(request.ArtifactOutputDirectory, "stdout.log"),
                stderrPath: Path.Combine(request.ArtifactOutputDirectory, "stderr.log"),
                resultArtifactPath: Path.Combine(request.ArtifactOutputDirectory, "result.md"),
                producedArtifactPaths:
                [
                    Path.Combine(request.ArtifactOutputDirectory, "stdout.log"),
                    Path.Combine(request.ArtifactOutputDirectory, "stderr.log"),
                    Path.Combine(request.ArtifactOutputDirectory, "result.md"),
                ]),
            MockRunnerBehavior.Fail => RunnerResult.Failed(options.ErrorSummary ?? "Mock runner failed.", exitCode: 1),
            MockRunnerBehavior.TimeOut => RunnerResult.TimedOut(options.ErrorSummary ?? "Mock runner timed out."),
            MockRunnerBehavior.RequireApproval => RunnerResult.ApprovalRequired(options.ErrorSummary ?? "Mock runner requires approval."),
            _ => throw new ArgumentOutOfRangeException(nameof(options), options.Behavior, "Unknown mock runner behavior."),
        };
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
