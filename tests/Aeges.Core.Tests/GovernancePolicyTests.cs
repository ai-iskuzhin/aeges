using Aeges.Core;

namespace Aeges.Core.Tests;

public sealed class GovernancePolicyTests
{
    [Fact]
    public void Create_records_limits_and_patterns()
    {
        var policy = new GovernancePolicy(
            maxIterations: 2,
            timeout: TimeSpan.FromMinutes(10),
            allowedPathPatterns: ["src/*"],
            deniedPathPatterns: ["src/Secrets/*"],
            approvalRequiredPathPatterns: ["*.csproj"],
            approvalRequiredCommandPatterns: ["git push"]);

        Assert.Equal(2, policy.MaxIterations);
        Assert.Equal(TimeSpan.FromMinutes(10), policy.Timeout);
        Assert.True(policy.IsPathAllowed("src/Program.cs"));
        Assert.True(policy.IsPathDenied("src/Secrets/token.txt"));
        Assert.True(policy.DoesPathRequireApproval("Aeges.Core.csproj"));
        Assert.True(policy.DoesCommandRequireApproval("git push origin main"));
    }

    [Theory]
    [InlineData("/absolute/path")]
    [InlineData("C:\\absolute\\path")]
    [InlineData("../outside")]
    [InlineData("src/../outside")]
    public void Create_rejects_unsafe_path_patterns(string pattern)
    {
        Assert.Throws<ArgumentException>(
            () => new GovernancePolicy(
                maxIterations: 1,
                timeout: TimeSpan.FromMinutes(1),
                allowedPathPatterns: [pattern]));
    }

    [Fact]
    public void Path_matching_supports_recursive_globs()
    {
        var policy = new GovernancePolicy(
            maxIterations: 1,
            timeout: TimeSpan.FromMinutes(1),
            approvalRequiredPathPatterns: ["**/Migrations/*", "**/*.csproj"]);

        Assert.True(policy.DoesPathRequireApproval("src/Aeges.Storage.Sqlite/Migrations/Initial.cs"));
        Assert.True(policy.DoesPathRequireApproval("src/Aeges.Core/Aeges.Core.csproj"));
        Assert.False(policy.DoesPathRequireApproval("src/Aeges.Core/RuntimeTask.cs"));
    }

    [Fact]
    public void Default_policy_requires_approval_for_known_sensitive_changes()
    {
        var policy = GovernancePolicy.CreateDefault();

        Assert.True(policy.DoesPathRequireApproval("package.json"));
        Assert.True(policy.DoesPathRequireApproval("src/Aeges.Core/Aeges.Core.csproj"));
        Assert.True(policy.DoesPathRequireApproval(".github/workflows/ci.yml"));
        Assert.True(policy.DoesCommandRequireApproval("git reset --hard HEAD"));
    }

    [Fact]
    public void GovernanceDecision_requires_reasons_for_non_allowed_decisions()
    {
        Assert.True(GovernanceDecision.Allow().IsAllowed);
        Assert.Throws<ArgumentException>(() => GovernanceDecision.RequireApproval([]));
        Assert.Throws<ArgumentException>(() => GovernanceDecision.Reject([" "]));
    }
}
