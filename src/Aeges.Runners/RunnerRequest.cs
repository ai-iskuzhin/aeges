using Aeges.Core;

namespace Aeges.Runners;

/// <summary>
/// Describes one governed runner execution request.
/// </summary>
public sealed class RunnerRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RunnerRequest"/> class.
    /// </summary>
    /// <param name="taskId">The task being executed.</param>
    /// <param name="iterationId">The iteration being executed.</param>
    /// <param name="projectId">The project that owns the task.</param>
    /// <param name="projectPath">The project root path.</param>
    /// <param name="worktreePath">The isolated worktree path where execution occurs.</param>
    /// <param name="promptPath">The prompt artifact path supplied to the runner.</param>
    /// <param name="artifactOutputDirectory">The directory where runner artifacts should be written.</param>
    /// <param name="timeout">The maximum allowed execution time.</param>
    /// <param name="environmentVariables">Environment variables supplied to the runner.</param>
    /// <param name="policyHints">Governance policy hints supplied to the runner.</param>
    /// <param name="sessionPolicy">The runner session continuity policy.</param>
    /// <param name="externalSessionId">The external runner session identifier to resume, when required.</param>
    public RunnerRequest(
        TaskId taskId,
        IterationId iterationId,
        ProjectId projectId,
        string projectPath,
        string worktreePath,
        string promptPath,
        string artifactOutputDirectory,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        IReadOnlyDictionary<string, string>? policyHints = null,
        RunnerSessionPolicy sessionPolicy = RunnerSessionPolicy.NewSession,
        string? externalSessionId = null)
    {
        TaskId = taskId;
        IterationId = iterationId;
        ProjectId = projectId;
        ProjectPath = RequireText(projectPath, nameof(projectPath));
        WorktreePath = RequireText(worktreePath, nameof(worktreePath));
        PromptPath = RequireText(promptPath, nameof(promptPath));
        ArtifactOutputDirectory = RequireText(artifactOutputDirectory, nameof(artifactOutputDirectory));
        Timeout = RequirePositive(timeout, nameof(timeout));
        EnvironmentVariables = CopyDictionary(environmentVariables);
        PolicyHints = CopyDictionary(policyHints);
        SessionPolicy = sessionPolicy;
        ExternalSessionId = NormalizeExternalSessionId(sessionPolicy, externalSessionId);
    }

    /// <summary>
    /// Gets the task being executed.
    /// </summary>
    public TaskId TaskId { get; }

    /// <summary>
    /// Gets the iteration being executed.
    /// </summary>
    public IterationId IterationId { get; }

    /// <summary>
    /// Gets the project that owns the task.
    /// </summary>
    public ProjectId ProjectId { get; }

    /// <summary>
    /// Gets the project root path.
    /// </summary>
    public string ProjectPath { get; }

    /// <summary>
    /// Gets the isolated worktree path where execution occurs.
    /// </summary>
    public string WorktreePath { get; }

    /// <summary>
    /// Gets the prompt artifact path supplied to the runner.
    /// </summary>
    public string PromptPath { get; }

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
        source is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(source);

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
