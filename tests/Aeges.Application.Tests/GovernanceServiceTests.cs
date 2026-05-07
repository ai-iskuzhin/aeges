using Aeges.Application.Governance;
using Aeges.Core;

namespace Aeges.Application.Tests;

public sealed class GovernanceServiceTests
{
    [Fact]
    public async Task EvaluateRunnerDispatchAsync_allows_dispatch_when_policy_passes()
    {
        var service = new GovernanceService();
        var policy = new GovernancePolicy(maxIterations: 3, timeout: TimeSpan.FromMinutes(30));

        var result = await service.EvaluateRunnerDispatchAsync(
            CreateRequest(policy: policy, plannedChangedPaths: ["src/Aeges.Core/RuntimeTask.cs"]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(GovernanceDecisionStatus.Allowed, result.Value.Status);
    }

    [Fact]
    public async Task EvaluateRunnerDispatchAsync_rejects_when_iteration_limit_is_reached()
    {
        var service = new GovernanceService();
        var policy = new GovernancePolicy(maxIterations: 2, timeout: TimeSpan.FromMinutes(30));

        var result = await service.EvaluateRunnerDispatchAsync(
            CreateRequest(policy: policy, currentIteration: 2),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(GovernanceDecisionStatus.Rejected, result.Value.Status);
        Assert.Contains(result.Value.Reasons, reason => reason.Contains("maximum of 2 iterations", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EvaluateRunnerDispatchAsync_rejects_when_timeout_exceeds_policy()
    {
        var service = new GovernanceService();
        var policy = new GovernancePolicy(maxIterations: 3, timeout: TimeSpan.FromMinutes(10));

        var result = await service.EvaluateRunnerDispatchAsync(
            CreateRequest(policy: policy, timeout: TimeSpan.FromMinutes(11)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(GovernanceDecisionStatus.Rejected, result.Value.Status);
        Assert.Contains(result.Value.Reasons, reason => reason.Contains("exceeds policy timeout", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EvaluateRunnerDispatchAsync_rejects_denied_and_outside_allowed_paths()
    {
        var service = new GovernanceService();
        var policy = new GovernancePolicy(
            maxIterations: 3,
            timeout: TimeSpan.FromMinutes(30),
            allowedPathPatterns: ["src/**"],
            deniedPathPatterns: ["src/Secrets/*"]);

        var result = await service.EvaluateRunnerDispatchAsync(
            CreateRequest(policy: policy, plannedChangedPaths: ["docs/readme.md", "src/Secrets/token.txt"]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(GovernanceDecisionStatus.Rejected, result.Value.Status);
        Assert.Contains(result.Value.Reasons, reason => reason.Contains("outside the allowed path policy", StringComparison.Ordinal));
        Assert.Contains(result.Value.Reasons, reason => reason.Contains("denied by policy", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EvaluateRunnerDispatchAsync_requires_approval_for_sensitive_paths_and_commands()
    {
        var service = new GovernanceService();
        var policy = GovernancePolicy.CreateDefault();

        var result = await service.EvaluateRunnerDispatchAsync(
            CreateRequest(
                policy: policy,
                plannedChangedPaths: ["src/Aeges.Core/Aeges.Core.csproj"],
                requestedCommands: ["git push origin main"]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(GovernanceDecisionStatus.ApprovalRequired, result.Value.Status);
        Assert.Contains(result.Value.Reasons, reason => reason.Contains("requires approval before modification", StringComparison.Ordinal));
        Assert.Contains(result.Value.Reasons, reason => reason.Contains("requires approval before execution", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EvaluateRunnerDispatchAsync_rejection_takes_priority_over_approval_required()
    {
        var service = new GovernanceService();
        var policy = new GovernancePolicy(
            maxIterations: 1,
            timeout: TimeSpan.FromMinutes(30),
            approvalRequiredPathPatterns: ["*.csproj"]);

        var result = await service.EvaluateRunnerDispatchAsync(
            CreateRequest(policy: policy, currentIteration: 1, plannedChangedPaths: ["Aeges.Core.csproj"]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(GovernanceDecisionStatus.Rejected, result.Value.Status);
    }

    [Fact]
    public async Task EvaluateRunnerDispatchAsync_returns_failure_for_invalid_request()
    {
        var service = new GovernanceService();

        var result = await service.EvaluateRunnerDispatchAsync(
            CreateRequest(currentIteration: -1),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_governance_request", result.Error?.Code);
    }

    [Fact]
    public async Task EvaluateRunnerDispatchAsync_rejects_unsafe_planned_path()
    {
        var service = new GovernanceService();

        var result = await service.EvaluateRunnerDispatchAsync(
            CreateRequest(plannedChangedPaths: ["../outside.txt"]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(GovernanceDecisionStatus.Rejected, result.Value.Status);
        Assert.Contains(result.Value.Reasons, reason => reason.Contains("is invalid", StringComparison.Ordinal));
    }

    private static RunnerDispatchGovernanceRequest CreateRequest(
        int currentIteration = 0,
        TimeSpan? timeout = null,
        IReadOnlyList<string>? plannedChangedPaths = null,
        IReadOnlyList<string>? requestedCommands = null,
        GovernancePolicy? policy = null) =>
        new(
            new TaskId("task-001"),
            new ProjectId("project-001"),
            currentIteration,
            timeout ?? TimeSpan.FromMinutes(10),
            plannedChangedPaths,
            requestedCommands,
            policy);
}
