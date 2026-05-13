namespace Aeges.Runners;

/// <summary>
/// Receives short progress events emitted by a governed runner while it is executing.
/// </summary>
public interface IRunnerProgressSink
{
    /// <summary>
    /// Reports a runner progress event.
    /// </summary>
    /// <param name="progressEvent">The progress event to report.</param>
    /// <param name="cancellationToken">A token that cancels progress reporting.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ReportAsync(RunnerProgressEvent progressEvent, CancellationToken cancellationToken);
}
