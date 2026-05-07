using Aeges.Core;

namespace Aeges.Application.Governance;

/// <summary>
/// Coordinates deterministic governance checks before runner dispatch.
/// </summary>
public sealed class GovernanceService
{
    /// <summary>
    /// Evaluates whether a runner dispatch may proceed.
    /// </summary>
    /// <param name="request">The runner dispatch governance request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A governance decision, or an expected validation failure.</returns>
    public Task<ApplicationResult<GovernanceDecision>> EvaluateRunnerDispatchAsync(
        RunnerDispatchGovernanceRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.CurrentIteration < 0)
        {
            return Task.FromResult(
                ApplicationResult<GovernanceDecision>.Failure(
                    "invalid_governance_request",
                    "CurrentIteration must not be negative."));
        }

        if (request.Timeout <= TimeSpan.Zero)
        {
            return Task.FromResult(
                ApplicationResult<GovernanceDecision>.Failure(
                    "invalid_governance_request",
                    "Timeout must be greater than zero."));
        }

        var policy = request.Policy ?? GovernancePolicy.CreateDefault();
        var rejectionReasons = new List<string>();
        var approvalReasons = new List<string>();

        if (request.CurrentIteration >= policy.MaxIterations)
        {
            rejectionReasons.Add($"Task '{request.TaskId}' has reached the maximum of {policy.MaxIterations} iterations.");
        }

        if (request.Timeout > policy.Timeout)
        {
            rejectionReasons.Add($"Requested timeout '{request.Timeout}' exceeds policy timeout '{policy.Timeout}'.");
        }

        EvaluatePaths(request.PlannedChangedPaths ?? [], policy, rejectionReasons, approvalReasons);
        EvaluateCommands(request.RequestedCommands ?? [], policy, approvalReasons);

        var decision = rejectionReasons.Count > 0
            ? GovernanceDecision.Reject(rejectionReasons)
            : approvalReasons.Count > 0
                ? GovernanceDecision.RequireApproval(approvalReasons)
                : GovernanceDecision.Allow();

        return Task.FromResult(ApplicationResult<GovernanceDecision>.Success(decision));
    }

    private static void EvaluatePaths(
        IEnumerable<string> plannedChangedPaths,
        GovernancePolicy policy,
        ICollection<string> rejectionReasons,
        ICollection<string> approvalReasons)
    {
        foreach (var path in plannedChangedPaths)
        {
            try
            {
                if (!policy.IsPathAllowed(path))
                {
                    rejectionReasons.Add($"Path '{path}' is outside the allowed path policy.");
                }

                if (policy.IsPathDenied(path))
                {
                    rejectionReasons.Add($"Path '{path}' is denied by policy.");
                }

                if (policy.DoesPathRequireApproval(path))
                {
                    approvalReasons.Add($"Path '{path}' requires approval before modification.");
                }
            }
            catch (ArgumentException exception)
            {
                rejectionReasons.Add($"Path '{path}' is invalid: {exception.Message}");
            }
        }
    }

    private static void EvaluateCommands(
        IEnumerable<string> requestedCommands,
        GovernancePolicy policy,
        ICollection<string> approvalReasons)
    {
        foreach (var command in requestedCommands)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                approvalReasons.Add("An empty command requires approval before dispatch.");
                continue;
            }

            if (policy.DoesCommandRequireApproval(command))
            {
                approvalReasons.Add($"Command '{command}' requires approval before execution.");
            }
        }
    }
}
