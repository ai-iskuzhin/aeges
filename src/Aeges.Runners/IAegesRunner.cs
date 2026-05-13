using Aeges.Core;

namespace Aeges.Runners;

/// <summary>
/// Defines an interchangeable backend that executes a governed task iteration.
/// </summary>
public interface IAegesRunner
{
    /// <summary>
    /// Gets the stable runner identifier.
    /// </summary>
    RunnerId Id { get; }

    /// <summary>
    /// Executes a governed runner request.
    /// </summary>
    /// <param name="request">The request describing the task iteration to execute.</param>
    /// <param name="cancellationToken">A token that cancels runner execution.</param>
    /// <returns>The runner execution result.</returns>
    Task<RunnerResult> RunAsync(RunnerRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Executes a governed runner request and optionally reports short progress events.
    /// </summary>
    /// <param name="request">The request describing the task iteration to execute.</param>
    /// <param name="progressSink">The optional progress sink.</param>
    /// <param name="cancellationToken">A token that cancels runner execution.</param>
    /// <returns>The runner execution result.</returns>
    Task<RunnerResult> RunAsync(
        RunnerRequest request,
        IRunnerProgressSink? progressSink,
        CancellationToken cancellationToken) =>
        RunAsync(request, cancellationToken);
}
