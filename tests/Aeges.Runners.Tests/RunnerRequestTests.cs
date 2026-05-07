using Aeges.Core;
using Aeges.Runners;

namespace Aeges.Runners.Tests;

public sealed class RunnerRequestTests
{
    [Fact]
    public void Create_records_runner_execution_inputs()
    {
        var request = CreateRequest(
            environmentVariables: new Dictionary<string, string>
            {
                ["AEGES_TEST"] = "true",
            },
            policyHints: new Dictionary<string, string>
            {
                ["allowed_paths"] = "src/*",
            });

        Assert.Equal(new TaskId("task-001"), request.TaskId);
        Assert.Equal(new IterationId("iteration-001"), request.IterationId);
        Assert.Equal(new ProjectId("project-001"), request.ProjectId);
        Assert.Equal("/work/aeges", request.ProjectPath);
        Assert.Equal("/tmp/aeges/worktrees/project-001/task-001", request.WorktreePath);
        Assert.Equal("/tmp/aeges/artifacts/task-001/prompt.md", request.PromptPath);
        Assert.Equal("/tmp/aeges/artifacts/task-001", request.ArtifactOutputDirectory);
        Assert.Equal(TimeSpan.FromMinutes(30), request.Timeout);
        Assert.Equal("true", request.EnvironmentVariables["AEGES_TEST"]);
        Assert.Equal("src/*", request.PolicyHints["allowed_paths"]);
    }

    [Fact]
    public void Create_copies_dictionaries()
    {
        var environment = new Dictionary<string, string>
        {
            ["AEGES_TEST"] = "true",
        };

        var request = CreateRequest(environmentVariables: environment);

        environment["AEGES_TEST"] = "false";

        Assert.Equal("true", request.EnvironmentVariables["AEGES_TEST"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_rejects_empty_paths(string value)
    {
        Assert.Throws<ArgumentException>(() => CreateRequest(projectPath: value));
        Assert.Throws<ArgumentException>(() => CreateRequest(worktreePath: value));
        Assert.Throws<ArgumentException>(() => CreateRequest(promptPath: value));
        Assert.Throws<ArgumentException>(() => CreateRequest(artifactOutputDirectory: value));
    }

    [Fact]
    public void Create_rejects_non_positive_timeout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRequest(timeout: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRequest(timeout: TimeSpan.FromSeconds(-1)));
    }

    private static RunnerRequest CreateRequest(
        string projectPath = "/work/aeges",
        string worktreePath = "/tmp/aeges/worktrees/project-001/task-001",
        string promptPath = "/tmp/aeges/artifacts/task-001/prompt.md",
        string artifactOutputDirectory = "/tmp/aeges/artifacts/task-001",
        TimeSpan? timeout = null,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        IReadOnlyDictionary<string, string>? policyHints = null) =>
        new(
            new TaskId("task-001"),
            new IterationId("iteration-001"),
            new ProjectId("project-001"),
            projectPath,
            worktreePath,
            promptPath,
            artifactOutputDirectory,
            timeout ?? TimeSpan.FromMinutes(30),
            environmentVariables,
            policyHints);
}
