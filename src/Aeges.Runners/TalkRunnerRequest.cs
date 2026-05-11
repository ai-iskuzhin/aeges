using Aeges.Core;

namespace Aeges.Runners;

/// <summary>
/// Describes one governed discussion request sent to a coding-agent runner.
/// </summary>
public sealed class TalkRunnerRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TalkRunnerRequest"/> class.
    /// </summary>
    /// <param name="sessionId">The discussion session identifier.</param>
    /// <param name="prompt">The prompt text to send to the runner.</param>
    /// <param name="workingDirectory">The working directory for the runner process.</param>
    /// <param name="artifactOutputDirectory">The directory where runner artifacts should be written.</param>
    /// <param name="timeout">The maximum allowed execution time.</param>
    /// <param name="environmentVariables">Environment variables supplied to the runner.</param>
    /// <param name="policyHints">Governance policy hints supplied to the runner.</param>
    /// <param name="sessionPolicy">The runner session continuity policy.</param>
    /// <param name="externalSessionId">The external runner session identifier to resume.</param>
    public TalkRunnerRequest(
        TalkSessionId sessionId,
        string prompt,
        string workingDirectory,
        string artifactOutputDirectory,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        IReadOnlyDictionary<string, string>? policyHints = null,
        RunnerSessionPolicy sessionPolicy = RunnerSessionPolicy.NewSession,
        string? externalSessionId = null)
    {
        SessionId = sessionId;
        Prompt = RequireText(prompt, nameof(prompt));
        WorkingDirectory = RequireText(workingDirectory, nameof(workingDirectory));
        ArtifactOutputDirectory = RequireText(artifactOutputDirectory, nameof(artifactOutputDirectory));
        Timeout = RequirePositive(timeout, nameof(timeout));
        EnvironmentVariables = CopyDictionary(environmentVariables);
        PolicyHints = CopyDictionary(policyHints);
        SessionPolicy = sessionPolicy;
        ExternalSessionId = NormalizeExternalSessionId(sessionPolicy, externalSessionId);
    }

    /// <summary>
    /// Gets the discussion session identifier.
    /// </summary>
    public TalkSessionId SessionId { get; }

    /// <summary>
    /// Gets the prompt text to send to the runner.
    /// </summary>
    public string Prompt { get; }

    /// <summary>
    /// Gets the runner process working directory.
    /// </summary>
    public string WorkingDirectory { get; }

    /// <summary>
    /// Gets the directory where runner artifacts should be written.
    /// </summary>
    public string ArtifactOutputDirectory { get; }

    /// <summary>
    /// Gets the maximum allowed execution time.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Gets environment variables supplied to the runner.
    /// </summary>
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; }

    /// <summary>
    /// Gets governance policy hints supplied to the runner.
    /// </summary>
    public IReadOnlyDictionary<string, string> PolicyHints { get; }

    /// <summary>
    /// Gets the runner session continuity policy.
    /// </summary>
    public RunnerSessionPolicy SessionPolicy { get; }

    /// <summary>
    /// Gets the external runner session identifier to resume.
    /// </summary>
    public string? ExternalSessionId { get; }

    private static IReadOnlyDictionary<string, string> CopyDictionary(IReadOnlyDictionary<string, string>? source) =>
        source is null ? new Dictionary<string, string>() : new Dictionary<string, string>(source);

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value;
    }

    private static TimeSpan RequirePositive(TimeSpan value, string parameterName)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Timeout must be greater than zero.");
        }

        return value;
    }

    private static string? NormalizeExternalSessionId(
        RunnerSessionPolicy sessionPolicy,
        string? externalSessionId)
    {
        return sessionPolicy switch
        {
            RunnerSessionPolicy.NewSession when externalSessionId is null => null,
            RunnerSessionPolicy.NewSession => throw new ArgumentException(
                "External session ID must not be set when starting a new runner session.",
                nameof(externalSessionId)),
            RunnerSessionPolicy.ResumeSession => RequireText(
                externalSessionId ?? string.Empty,
                nameof(externalSessionId)),
            _ => throw new ArgumentOutOfRangeException(nameof(sessionPolicy), sessionPolicy, "Unknown session policy."),
        };
    }
}
