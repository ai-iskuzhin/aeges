using Aeges.Core;

namespace Aeges.Application.Governance;

/// <summary>
/// Describes the governed inputs checked before runner dispatch.
/// </summary>
/// <param name="TaskId">The task being dispatched.</param>
/// <param name="ProjectId">The project that owns the task.</param>
/// <param name="CurrentIteration">The current zero-based task iteration count.</param>
/// <param name="Timeout">The requested runner timeout.</param>
/// <param name="PlannedChangedPaths">The project-relative paths the runner is expected to change.</param>
/// <param name="RequestedCommands">The commands the runtime expects to permit for the runner.</param>
/// <param name="Policy">The governance policy to evaluate. The default policy is used when omitted.</param>
public sealed record RunnerDispatchGovernanceRequest(
    TaskId TaskId,
    ProjectId ProjectId,
    int CurrentIteration,
    TimeSpan Timeout,
    IReadOnlyList<string>? PlannedChangedPaths = null,
    IReadOnlyList<string>? RequestedCommands = null,
    GovernancePolicy? Policy = null);
