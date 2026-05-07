namespace Aeges.Runners;

/// <summary>
/// Represents the durable result metadata produced by a runner execution.
/// </summary>
public sealed class RunnerResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RunnerResult"/> class.
    /// </summary>
    /// <param name="status">The runner execution status.</param>
    /// <param name="exitCode">The process exit code, when one exists.</param>
    /// <param name="stdoutPath">The captured stdout artifact path, when produced.</param>
    /// <param name="stderrPath">The captured stderr artifact path, when produced.</param>
    /// <param name="resultArtifactPath">The result artifact path, when produced.</param>
    /// <param name="producedArtifactPaths">Additional artifact paths produced by the runner.</param>
    /// <param name="errorSummary">A short error summary for failed executions.</param>
    public RunnerResult(
        RunnerStatus status,
        int? exitCode = null,
        string? stdoutPath = null,
        string? stderrPath = null,
        string? resultArtifactPath = null,
        IReadOnlyCollection<string>? producedArtifactPaths = null,
        string? errorSummary = null)
    {
        Status = status;
        ExitCode = exitCode;
        StdoutPath = RequireOptionalPath(stdoutPath, nameof(stdoutPath));
        StderrPath = RequireOptionalPath(stderrPath, nameof(stderrPath));
        ResultArtifactPath = RequireOptionalPath(resultArtifactPath, nameof(resultArtifactPath));
        ProducedArtifactPaths = CopyPaths(producedArtifactPaths);
        ErrorSummary = RequireOptionalText(errorSummary, nameof(errorSummary));
    }

    /// <summary>
    /// Gets the runner execution status.
    /// </summary>
    public RunnerStatus Status { get; }

    /// <summary>
    /// Gets the process exit code, when one exists.
    /// </summary>
    public int? ExitCode { get; }

    /// <summary>
    /// Gets the captured stdout artifact path, when produced.
    /// </summary>
    public string? StdoutPath { get; }

    /// <summary>
    /// Gets the captured stderr artifact path, when produced.
    /// </summary>
    public string? StderrPath { get; }

    /// <summary>
    /// Gets the result artifact path, when produced.
    /// </summary>
    public string? ResultArtifactPath { get; }

    /// <summary>
    /// Gets additional artifact paths produced by the runner.
    /// </summary>
    public IReadOnlyCollection<string> ProducedArtifactPaths { get; }

    /// <summary>
    /// Gets a short error summary for failed executions.
    /// </summary>
    public string? ErrorSummary { get; }

    /// <summary>
    /// Creates a successful runner result.
    /// </summary>
    /// <param name="exitCode">The runner process exit code.</param>
    /// <param name="stdoutPath">The captured stdout artifact path, when produced.</param>
    /// <param name="stderrPath">The captured stderr artifact path, when produced.</param>
    /// <param name="resultArtifactPath">The result artifact path, when produced.</param>
    /// <param name="producedArtifactPaths">Additional artifact paths produced by the runner.</param>
    /// <returns>A successful runner result.</returns>
    public static RunnerResult Succeeded(
        int exitCode = 0,
        string? stdoutPath = null,
        string? stderrPath = null,
        string? resultArtifactPath = null,
        IReadOnlyCollection<string>? producedArtifactPaths = null) =>
        new(
            RunnerStatus.Succeeded,
            exitCode,
            stdoutPath,
            stderrPath,
            resultArtifactPath,
            producedArtifactPaths);

    /// <summary>
    /// Creates a failed runner result.
    /// </summary>
    /// <param name="errorSummary">A short failure summary.</param>
    /// <param name="exitCode">The runner process exit code, when one exists.</param>
    /// <param name="stdoutPath">The captured stdout artifact path, when produced.</param>
    /// <param name="stderrPath">The captured stderr artifact path, when produced.</param>
    /// <returns>A failed runner result.</returns>
    public static RunnerResult Failed(
        string errorSummary,
        int? exitCode = null,
        string? stdoutPath = null,
        string? stderrPath = null) =>
        new(RunnerStatus.Failed, exitCode, stdoutPath, stderrPath, errorSummary: errorSummary);

    /// <summary>
    /// Creates a timed-out runner result.
    /// </summary>
    /// <param name="errorSummary">A short timeout summary.</param>
    /// <returns>A timed-out runner result.</returns>
    public static RunnerResult TimedOut(string errorSummary) =>
        new(RunnerStatus.TimedOut, errorSummary: errorSummary);

    /// <summary>
    /// Creates a cancelled runner result.
    /// </summary>
    /// <param name="errorSummary">A short cancellation summary.</param>
    /// <returns>A cancelled runner result.</returns>
    public static RunnerResult Cancelled(string errorSummary) =>
        new(RunnerStatus.Cancelled, errorSummary: errorSummary);

    /// <summary>
    /// Creates a runner result indicating an approval gate is required.
    /// </summary>
    /// <param name="errorSummary">A short approval summary.</param>
    /// <returns>An approval-required runner result.</returns>
    public static RunnerResult ApprovalRequired(string errorSummary) =>
        new(RunnerStatus.ApprovalRequired, errorSummary: errorSummary);

    private static IReadOnlyCollection<string> CopyPaths(IReadOnlyCollection<string>? source)
    {
        if (source is null)
        {
            return [];
        }

        return source.Select((path, index) => RequireOptionalPath(path, $"producedArtifactPaths[{index}]")!).ToArray();
    }

    private static string? RequireOptionalPath(string? value, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Path must not be empty.", parameterName);
        }

        return value;
    }

    private static string? RequireOptionalText(string? value, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value;
    }
}
