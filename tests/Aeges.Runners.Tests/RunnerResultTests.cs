using Aeges.Runners;

namespace Aeges.Runners.Tests;

public sealed class RunnerResultTests
{
    [Fact]
    public void Succeeded_creates_success_result()
    {
        var result = RunnerResult.Succeeded(
            stdoutPath: "stdout.log",
            stderrPath: "stderr.log",
            resultArtifactPath: "result.md",
            producedArtifactPaths: ["stdout.log", "stderr.log", "result.md"],
            externalSessionId: "thread-001");

        Assert.Equal(RunnerStatus.Succeeded, result.Status);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("stdout.log", result.StdoutPath);
        Assert.Equal("stderr.log", result.StderrPath);
        Assert.Equal("result.md", result.ResultArtifactPath);
        Assert.Equal(["stdout.log", "stderr.log", "result.md"], result.ProducedArtifactPaths);
        Assert.Equal("thread-001", result.ExternalSessionId);
        Assert.Null(result.ErrorSummary);
    }

    [Fact]
    public void Failed_requires_error_summary()
    {
        var result = RunnerResult.Failed("Codex exited with code 1.", exitCode: 1);

        Assert.Equal(RunnerStatus.Failed, result.Status);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal("Codex exited with code 1.", result.ErrorSummary);
        Assert.Throws<ArgumentException>(() => RunnerResult.Failed(" "));
    }

    [Fact]
    public void Interrupted_results_record_summary()
    {
        var timedOut = RunnerResult.TimedOut("Runner exceeded timeout.");
        var cancelled = RunnerResult.Cancelled("Runner was cancelled.");

        Assert.Equal(RunnerStatus.TimedOut, timedOut.Status);
        Assert.Equal("Runner exceeded timeout.", timedOut.ErrorSummary);
        Assert.True(timedOut.Status.IsInterrupted());
        Assert.Equal(RunnerStatus.Cancelled, cancelled.Status);
        Assert.Equal("Runner was cancelled.", cancelled.ErrorSummary);
        Assert.True(cancelled.Status.IsInterrupted());
    }

    [Fact]
    public void ApprovalRequired_records_status()
    {
        var result = RunnerResult.ApprovalRequired("Dependency change requires approval.");

        Assert.Equal(RunnerStatus.ApprovalRequired, result.Status);
        Assert.Equal("Dependency change requires approval.", result.ErrorSummary);
    }

    [Fact]
    public void Result_rejects_empty_paths()
    {
        Assert.Throws<ArgumentException>(() => RunnerResult.Succeeded(stdoutPath: " "));
        Assert.Throws<ArgumentException>(() => RunnerResult.Succeeded(stderrPath: " "));
        Assert.Throws<ArgumentException>(() => RunnerResult.Succeeded(resultArtifactPath: " "));
        Assert.Throws<ArgumentException>(() => RunnerResult.Succeeded(producedArtifactPaths: ["artifact.md", " "]));
    }

    [Theory]
    [InlineData(RunnerStatus.Succeeded, "succeeded")]
    [InlineData(RunnerStatus.Failed, "failed")]
    [InlineData(RunnerStatus.TimedOut, "timed_out")]
    [InlineData(RunnerStatus.Cancelled, "cancelled")]
    [InlineData(RunnerStatus.ApprovalRequired, "approval_required")]
    public void Status_round_trips_storage_values(RunnerStatus status, string storageValue)
    {
        Assert.Equal(storageValue, status.ToStorageValue());
        Assert.Equal(status, RunnerStatusExtensions.FromStorageValue(storageValue));
    }
}
