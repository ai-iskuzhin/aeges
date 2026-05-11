using Aeges.Core;
using Aeges.Runners;
using Aeges.Runners.Codex;

namespace Aeges.Runners.Tests;

public sealed class CodexTalkRunnerTests
{
    [Fact]
    public async Task SendAsync_succeeds_and_extracts_response_and_thread_id()
    {
        using var artifacts = new TemporaryArtifactDirectory();
        var executor = new FakeCodexCommandExecutor(
            CodexCommandExecutionResult.Exited(0),
            """
            {"type":"thread.started","thread_id":"thread-001"}
            {"type":"item.completed","item":{"type":"agent_message","text":"Hello from Codex talk."}}
            """);
        var runner = new CodexTalkRunner(
            commandExecutor: executor,
            executableResolver: new FakeCodexExecutableResolver("/usr/local/bin/codex"));

        var result = await runner.SendAsync(CreateRequest(artifacts.Path), CancellationToken.None);

        Assert.Equal(RunnerStatus.Succeeded, result.Status);
        Assert.Equal("Hello from Codex talk.", result.ResponseText);
        Assert.Equal("thread-001", result.ExternalSessionId);
        Assert.True(File.Exists(result.ResponseArtifactPath));
        Assert.Contains("read-only", executor.LastCommand!.Arguments);
        Assert.Contains("--skip-git-repo-check", executor.LastCommand.Arguments);
        Assert.Equal("Discuss Aeges.", executor.LastCommand.StandardInput);
    }

    [Fact]
    public async Task SendAsync_reports_missing_codex_without_launching()
    {
        using var artifacts = new TemporaryArtifactDirectory();
        var executor = new FakeCodexCommandExecutor(CodexCommandExecutionResult.Exited(0));
        var runner = new CodexTalkRunner(
            commandExecutor: executor,
            executableResolver: new FakeCodexExecutableResolver(resolvedPath: null));

        var result = await runner.SendAsync(CreateRequest(artifacts.Path), CancellationToken.None);

        Assert.Equal(RunnerStatus.Failed, result.Status);
        Assert.Contains(CodexRunnerAvailability.CodexProjectUrl, result.ErrorSummary, StringComparison.Ordinal);
        Assert.Null(executor.LastCommand);
    }

    private static TalkRunnerRequest CreateRequest(string artifactOutputDirectory) =>
        new(
            new TalkSessionId("talk-001"),
            "Discuss Aeges.",
            "/tmp/aeges",
            artifactOutputDirectory,
            TimeSpan.FromMinutes(5));

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
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"aeges-codex-talk-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
