using Aeges.Core;

namespace Aeges.Application.RunnerExecutions;

/// <summary>
/// Describes a request to start tracking a runner process execution.
/// </summary>
/// <param name="TaskId">The owning task identifier.</param>
/// <param name="IterationId">The owning iteration identifier.</param>
/// <param name="RunnerId">The runner implementation that is being launched.</param>
/// <param name="Command">The command description used to launch the runner.</param>
/// <param name="WorkingDirectory">The runner working directory.</param>
/// <param name="RunnerExecutionId">The optional caller-provided runner execution identifier.</param>
public sealed record StartRunnerExecutionRequest(
    TaskId TaskId,
    IterationId IterationId,
    RunnerId RunnerId,
    string Command,
    string WorkingDirectory,
    RunnerExecutionId? RunnerExecutionId = null);
