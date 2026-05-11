namespace Aeges.Runners;

/// <summary>
/// Represents the result of one governed discussion runner turn.
/// </summary>
public sealed class TalkRunnerResult
{
    private TalkRunnerResult(
        RunnerStatus status,
        string? responseText,
        int? exitCode,
        string? stdoutPath,
        string? stderrPath,
        string? responseArtifactPath,
        string? errorSummary,
        string? externalSessionId)
    {
        Status = status;
        ResponseText = RequireOptionalText(responseText, nameof(responseText));
        ExitCode = exitCode;
        StdoutPath = RequireOptionalPath(stdoutPath, nameof(stdoutPath));
        StderrPath = RequireOptionalPath(stderrPath, nameof(stderrPath));
        ResponseArtifactPath = RequireOptionalPath(responseArtifactPath, nameof(responseArtifactPath));
        ErrorSummary = RequireOptionalText(errorSummary, nameof(errorSummary));
        ExternalSessionId = RequireOptionalText(externalSessionId, nameof(externalSessionId));
    }

    /// <summary>
    /// Gets the runner execution status.
    /// </summary>
    public RunnerStatus Status { get; }

    /// <summary>
    /// Gets the assistant response text.
    /// </summary>
    public string? ResponseText { get; }

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
    /// Gets the persisted assistant response artifact path, when produced.
    /// </summary>
    public string? ResponseArtifactPath { get; }

    /// <summary>
    /// Gets a short error summary for failed executions.
    /// </summary>
    public string? ErrorSummary { get; }

    /// <summary>
    /// Gets the external runner session identifier produced or reused by the execution.
    /// </summary>
    public string? ExternalSessionId { get; }

    /// <summary>
    /// Creates a successful talk runner result.
    /// </summary>
    /// <param name="responseText">The assistant response text.</param>
    /// <param name="exitCode">The runner process exit code.</param>
    /// <param name="stdoutPath">The captured stdout artifact path.</param>
    /// <param name="stderrPath">The captured stderr artifact path.</param>
    /// <param name="responseArtifactPath">The persisted response artifact path.</param>
    /// <param name="externalSessionId">The external runner session identifier.</param>
    /// <returns>A successful talk runner result.</returns>
    public static TalkRunnerResult Succeeded(
        string responseText,
        int exitCode = 0,
        string? stdoutPath = null,
        string? stderrPath = null,
        string? responseArtifactPath = null,
        string? externalSessionId = null) =>
        new(
            RunnerStatus.Succeeded,
            responseText,
            exitCode,
            stdoutPath,
            stderrPath,
            responseArtifactPath,
            errorSummary: null,
            externalSessionId);

    /// <summary>
    /// Creates a failed talk runner result.
    /// </summary>
    /// <param name="errorSummary">A short failure summary.</param>
    /// <param name="exitCode">The process exit code, when one exists.</param>
    /// <param name="stdoutPath">The captured stdout artifact path.</param>
    /// <param name="stderrPath">The captured stderr artifact path.</param>
    /// <param name="externalSessionId">The external runner session identifier.</param>
    /// <returns>A failed talk runner result.</returns>
    public static TalkRunnerResult Failed(
        string errorSummary,
        int? exitCode = null,
        string? stdoutPath = null,
        string? stderrPath = null,
        string? externalSessionId = null) =>
        new(RunnerStatus.Failed, null, exitCode, stdoutPath, stderrPath, null, errorSummary, externalSessionId);

    private static string? RequireOptionalPath(string? value, string parameterName) =>
        RequireOptionalText(value, parameterName);

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
