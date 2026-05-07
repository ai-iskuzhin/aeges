using Aeges.Core;
using Aeges.Runners;
using Aeges.Runners.Codex;

namespace Aeges.Runners.Tests;

public sealed class CodexRunnerCommandBuilderTests
{
    [Fact]
    public void Build_creates_codex_command_description()
    {
        var builder = new CodexRunnerCommandBuilder(
            new CodexRunnerOptions(
                Executable: "codex-test",
                BaseArguments: ["exec", "--json"],
                EnvironmentVariables: new Dictionary<string, string>
                {
                    ["CODEX_HOME"] = "/tmp/codex",
                }));

        var command = builder.Build(CreateRequest());

        Assert.Equal("codex-test", command.Executable);
        Assert.Equal(["exec", "--json", "/tmp/aeges/artifacts/task-001/prompt.md"], command.Arguments);
        Assert.Equal("/tmp/aeges/worktrees/project-001/task-001", command.WorkingDirectory);
        Assert.Equal(TimeSpan.FromMinutes(30), command.Timeout);
        Assert.Equal("/tmp/codex", command.EnvironmentVariables["CODEX_HOME"]);
        Assert.Equal("true", command.EnvironmentVariables["AEGES_TEST"]);
        Assert.Equal("task-001", command.EnvironmentVariables["AEGES_TASK_ID"]);
        Assert.Equal("iteration-001", command.EnvironmentVariables["AEGES_ITERATION_ID"]);
        Assert.Equal("project-001", command.EnvironmentVariables["AEGES_PROJECT_ID"]);
        Assert.Equal("/tmp/aeges/artifacts/task-001", command.EnvironmentVariables["AEGES_ARTIFACT_OUTPUT_DIRECTORY"]);
        Assert.Equal("src/*", command.EnvironmentVariables["AEGES_POLICY_ALLOWED_PATHS"]);
    }

    [Fact]
    public void Build_uses_default_codex_exec_contract()
    {
        var command = new CodexRunnerCommandBuilder().Build(CreateRequest());

        Assert.Equal("codex", command.Executable);
        Assert.Equal(["exec", "/tmp/aeges/artifacts/task-001/prompt.md"], command.Arguments);
    }

    [Fact]
    public void Create_rejects_invalid_options()
    {
        Assert.Throws<ArgumentException>(() => new CodexRunnerCommandBuilder(new CodexRunnerOptions(Executable: " ")));
        Assert.Throws<ArgumentException>(() => new CodexRunnerCommandBuilder(new CodexRunnerOptions(BaseArguments: ["exec", " "])));
        Assert.Throws<ArgumentException>(
            () => new CodexRunnerCommandBuilder(
                new CodexRunnerOptions(
                    EnvironmentVariables: new Dictionary<string, string>
                    {
                        [" "] = "value",
                    })));
    }

    private static RunnerRequest CreateRequest() =>
        new(
            new TaskId("task-001"),
            new IterationId("iteration-001"),
            new ProjectId("project-001"),
            "/work/aeges",
            "/tmp/aeges/worktrees/project-001/task-001",
            "/tmp/aeges/artifacts/task-001/prompt.md",
            "/tmp/aeges/artifacts/task-001",
            TimeSpan.FromMinutes(30),
            environmentVariables: new Dictionary<string, string>
            {
                ["AEGES_TEST"] = "true",
            },
            policyHints: new Dictionary<string, string>
            {
                ["allowed_paths"] = "src/*",
            });
}
