using Aeges.Core;
using Aeges.Runners;
using Aeges.Runners.Codex;

namespace Aeges.Runners.Tests;

public sealed class CodexRunnerTests
{
    [Fact]
    public async Task RunAsync_succeeds_and_captures_thread_id()
    {
        using var artifacts = new TemporaryArtifactDirectory();
        var executor = new FakeCodexCommandExecutor(
            CodexCommandExecutionResult.Exited(0),
            """{"type":"thread.started","thread_id":"thread-001"}""");
        var runner = CreateRunner(executor);

        var result = await runner.RunAsync(CreateRequest(artifacts.Path, artifacts.PromptPath), CancellationToken.None);

        Assert.Equal(new RunnerId("codex"), runner.Id);
        Assert.Equal(RunnerStatus.Succeeded, result.Status);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("thread-001", result.ExternalSessionId);
        Assert.True(File.Exists(result.StdoutPath));
        Assert.True(File.Exists(result.StderrPath));
        Assert.Contains(result.StdoutPath!, result.ProducedArtifactPaths);
        Assert.Equal("codex", executor.LastCommand?.Executable);
        Assert.Equal(artifacts.PromptText, executor.LastCommand?.StandardInput);
    }

    [Fact]
    public async Task RunAsync_reports_non_zero_exit_as_failure()
    {
        using var artifacts = new TemporaryArtifactDirectory();
        var runner = CreateRunner(new FakeCodexCommandExecutor(CodexCommandExecutionResult.Exited(2)));

        var result = await runner.RunAsync(CreateRequest(artifacts.Path, artifacts.PromptPath), CancellationToken.None);

        Assert.Equal(RunnerStatus.Failed, result.Status);
        Assert.Equal(2, result.ExitCode);
        Assert.Equal("Codex exited with code 2.", result.ErrorSummary);
        Assert.True(File.Exists(result.StdoutPath));
    }

    [Fact]
    public async Task RunAsync_reports_timeout()
    {
        using var artifacts = new TemporaryArtifactDirectory();
        var runner = CreateRunner(new FakeCodexCommandExecutor(
            CodexCommandExecutionResult.Timeout("Codex runner exceeded its timeout.")));

        var result = await runner.RunAsync(CreateRequest(artifacts.Path, artifacts.PromptPath), CancellationToken.None);

        Assert.Equal(RunnerStatus.TimedOut, result.Status);
        Assert.Equal("Codex runner exceeded its timeout.", result.ErrorSummary);
        Assert.True(File.Exists(result.StdoutPath));
    }

    [Fact]
    public async Task RunAsync_reports_missing_codex_without_launching()
    {
        using var artifacts = new TemporaryArtifactDirectory();
        var executor = new FakeCodexCommandExecutor(CodexCommandExecutionResult.Exited(0));
        var runner = new CodexRunner(
            commandExecutor: executor,
            executableResolver: new FakeCodexExecutableResolver(resolvedPath: null));

        var result = await runner.RunAsync(CreateRequest(artifacts.Path, artifacts.PromptPath), CancellationToken.None);

        Assert.Equal(RunnerStatus.Failed, result.Status);
        Assert.Contains(CodexRunnerAvailability.CodexProjectUrl, result.ErrorSummary, StringComparison.Ordinal);
        Assert.Null(executor.LastCommand);
    }

    private static CodexRunner CreateRunner(FakeCodexCommandExecutor executor) =>
        new(
            commandExecutor: executor,
            executableResolver: new FakeCodexExecutableResolver("/usr/local/bin/codex"));

    private static RunnerRequest CreateRequest(string artifactOutputDirectory, string promptPath) =>
        new(
            new TaskId("task-001"),
            new IterationId("iteration-001"),
            new ProjectId("project-001"),
            "/work/aeges",
            "/tmp/aeges/worktrees/project-001/task-001",
            promptPath,
            artifactOutputDirectory,
            TimeSpan.FromMinutes(30));

    private sealed class FakeCodexCommandExecutor : ICodexCommandExecutor
    {
        private readonly CodexCommandExecutionResult result;
        private readonly string stdout;

        public FakeCodexCommandExecutor(CodexCommandExecutionResult result, string stdout = "")
        {
            this.result = result;
            this.stdout = stdout;
        }

        public CodexRunnerCommand? LastCommand { get; private set; }

        public async Task<CodexCommandExecutionResult> ExecuteAsync(
            CodexRunnerCommand command,
            string stdoutPath,
            string stderrPath,
            CancellationToken cancellationToken)
        {
            LastCommand = command;
            Directory.CreateDirectory(Path.GetDirectoryName(stdoutPath)!);
            await File.WriteAllTextAsync(stdoutPath, stdout, cancellationToken);
            await File.WriteAllTextAsync(stderrPath, string.Empty, cancellationToken);

            return result;
        }
    }

    private sealed class FakeCodexExecutableResolver : ICodexExecutableResolver
    {
        private readonly string? resolvedPath;

        public FakeCodexExecutableResolver(string? resolvedPath)
        {
            this.resolvedPath = resolvedPath;
        }

        public string? Resolve(string executable) => resolvedPath;
    }

    private sealed class TemporaryArtifactDirectory : IDisposable
    {
        public TemporaryArtifactDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"aeges-codex-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
            PromptPath = System.IO.Path.Combine(Path, "prompt.md");
            File.WriteAllText(PromptPath, PromptText);
        }

        public string Path { get; }

        public string PromptPath { get; }

        public string PromptText { get; } = "Run the governed Codex smoke task.";

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
