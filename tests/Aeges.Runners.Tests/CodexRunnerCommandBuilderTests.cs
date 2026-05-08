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
                Model: "gpt-5.5",
                ReasoningEffort: "high",
                EnvironmentVariables: new Dictionary<string, string>
                {
                    ["CODEX_HOME"] = "/tmp/codex",
                }),
            new FakeCodexExecutableResolver("/usr/local/bin/codex-test"));

        var command = builder.Build(CreateRequest());

        Assert.Equal("codex-test", command.Executable);
        Assert.Equal(
            [
                "exec",
                "--json",
                "--model",
                "gpt-5.5",
                "--config",
                "model_reasoning_effort=\"high\"",
                "/tmp/aeges/artifacts/task-001/prompt.md",
            ],
            command.Arguments);
        Assert.Equal("/tmp/aeges/worktrees/project-001/task-001", command.WorkingDirectory);
        Assert.Equal(TimeSpan.FromMinutes(30), command.Timeout);
        Assert.Equal("/tmp/codex", command.EnvironmentVariables["CODEX_HOME"]);
        Assert.Equal("true", command.EnvironmentVariables["AEGES_TEST"]);
        Assert.Equal("task-001", command.EnvironmentVariables["AEGES_TASK_ID"]);
        Assert.Equal("iteration-001", command.EnvironmentVariables["AEGES_ITERATION_ID"]);
        Assert.Equal("project-001", command.EnvironmentVariables["AEGES_PROJECT_ID"]);
        Assert.Equal("/tmp/aeges/artifacts/task-001", command.EnvironmentVariables["AEGES_ARTIFACT_OUTPUT_DIRECTORY"]);
        Assert.Equal(RunnerSessionPolicy.NewSession.ToString(), command.EnvironmentVariables["AEGES_RUNNER_SESSION_POLICY"]);
        Assert.Equal("src/*", command.EnvironmentVariables["AEGES_POLICY_ALLOWED_PATHS"]);
    }

    [Fact]
    public void Build_uses_default_codex_exec_contract()
    {
        var command = new CodexRunnerCommandBuilder(
            executableResolver: new FakeCodexExecutableResolver("/usr/local/bin/codex")).Build(CreateRequest());

        Assert.Equal("codex", command.Executable);
        Assert.Equal(["exec", "/tmp/aeges/artifacts/task-001/prompt.md"], command.Arguments);
    }

    [Fact]
    public void Build_creates_codex_resume_command_for_explicit_external_session()
    {
        var command = new CodexRunnerCommandBuilder(
            new CodexRunnerOptions(Model: "gpt-5.5", ReasoningEffort: "low"),
            new FakeCodexExecutableResolver("/usr/local/bin/codex"))
            .Build(CreateRequest(
                sessionPolicy: RunnerSessionPolicy.ResumeSession,
                externalSessionId: "019e05e0-d00b-7182-8516-0d258c7993aa"));

        Assert.Equal(
            [
                "exec",
                "resume",
                "--model",
                "gpt-5.5",
                "--config",
                "model_reasoning_effort=\"low\"",
                "019e05e0-d00b-7182-8516-0d258c7993aa",
                "/tmp/aeges/artifacts/task-001/prompt.md",
            ],
            command.Arguments);
        Assert.Equal(RunnerSessionPolicy.ResumeSession.ToString(), command.EnvironmentVariables["AEGES_RUNNER_SESSION_POLICY"]);
        Assert.Equal(
            "019e05e0-d00b-7182-8516-0d258c7993aa",
            command.EnvironmentVariables["AEGES_EXTERNAL_SESSION_ID"]);
    }

    [Fact]
    public void Build_rejects_missing_codex_executable_with_installation_link()
    {
        var builder = new CodexRunnerCommandBuilder(
            executableResolver: new FakeCodexExecutableResolver(resolvedPath: null));

        var exception = Assert.Throws<CodexRunnerUnavailableException>(() => builder.Build(CreateRequest()));

        Assert.False(exception.Availability.IsAvailable);
        Assert.Equal("codex", exception.Availability.Executable);
        Assert.Contains("Codex CLI executable 'codex' was not found", exception.Message, StringComparison.Ordinal);
        Assert.Contains(CodexRunnerAvailability.CodexProjectUrl, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckAvailability_reports_resolved_codex_executable()
    {
        var builder = new CodexRunnerCommandBuilder(
            executableResolver: new FakeCodexExecutableResolver("/usr/local/bin/codex"));

        var availability = builder.CheckAvailability();

        Assert.True(availability.IsAvailable);
        Assert.Equal("codex", availability.Executable);
        Assert.Equal("/usr/local/bin/codex", availability.ResolvedPath);
    }

    [Fact]
    public void Create_rejects_invalid_options()
    {
        Assert.Throws<ArgumentException>(() => new CodexRunnerCommandBuilder(new CodexRunnerOptions(Executable: " ")));
        Assert.Throws<ArgumentException>(() => new CodexRunnerCommandBuilder(new CodexRunnerOptions(BaseArguments: ["exec", " "])));
        Assert.Throws<ArgumentException>(() => new CodexRunnerCommandBuilder(new CodexRunnerOptions(Model: " ")));
        Assert.Throws<ArgumentException>(() => new CodexRunnerCommandBuilder(new CodexRunnerOptions(ReasoningEffort: " ")));
        Assert.Throws<ArgumentException>(
            () => new CodexRunnerCommandBuilder(
                new CodexRunnerOptions(
                    EnvironmentVariables: new Dictionary<string, string>
                    {
                        [" "] = "value",
                    })));
    }

    [Fact]
    public void PathResolver_finds_installed_codex_cli_when_available()
    {
        var resolvedPath = new PathCodexExecutableResolver().Resolve("codex");

        if (resolvedPath is null)
        {
            return;
        }

        Assert.True(File.Exists(resolvedPath));
    }

    [Fact]
    public void JsonEvents_reads_thread_started_id()
    {
        var found = CodexRunnerJsonEvents.TryReadThreadId(
            """{"type":"thread.started","thread_id":"019e05e0-d00b-7182-8516-0d258c7993aa"}""",
            out var threadId);

        Assert.True(found);
        Assert.Equal("019e05e0-d00b-7182-8516-0d258c7993aa", threadId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("""{"type":"turn.started"}""")]
    public void JsonEvents_ignores_non_thread_started_lines(string jsonLine)
    {
        var found = CodexRunnerJsonEvents.TryReadThreadId(jsonLine, out var threadId);

        Assert.False(found);
        Assert.Null(threadId);
    }

    private static RunnerRequest CreateRequest(
        RunnerSessionPolicy sessionPolicy = RunnerSessionPolicy.NewSession,
        string? externalSessionId = null) =>
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
            },
            sessionPolicy: sessionPolicy,
            externalSessionId: externalSessionId);

    private sealed class FakeCodexExecutableResolver : ICodexExecutableResolver
    {
        private readonly string? resolvedPath;

        public FakeCodexExecutableResolver(string? resolvedPath)
        {
            this.resolvedPath = resolvedPath;
        }

        public string? Resolve(string executable) => resolvedPath;
    }
}
