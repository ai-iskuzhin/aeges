using Aeges.Core;

namespace Aeges.Application.RunnerDispatch;

/// <summary>
/// Describes a request to build a governed runner request for one task iteration.
/// </summary>
/// <param name="Task">The task being executed.</param>
/// <param name="Project">The project that owns the task.</param>
/// <param name="Iteration">The iteration being executed.</param>
/// <param name="RuntimeLayout">The local runtime directory layout.</param>
/// <param name="Timeout">The maximum allowed runner execution time.</param>
/// <param name="EnvironmentVariables">Environment variables supplied to the runner.</param>
/// <param name="PolicyHints">Governance policy hints supplied to the runner.</param>
public sealed record CreateRunnerRequestRequest(
    RuntimeTask Task,
    RuntimeProject Project,
    TaskIteration Iteration,
    Runtime.RuntimeDirectoryLayout RuntimeLayout,
    TimeSpan Timeout,
    IReadOnlyDictionary<string, string>? EnvironmentVariables = null,
    IReadOnlyDictionary<string, string>? PolicyHints = null);
