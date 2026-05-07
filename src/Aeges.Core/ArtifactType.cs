namespace Aeges.Core;

/// <summary>
/// Defines the durable artifact categories produced or consumed by the runtime.
/// </summary>
public enum ArtifactType
{
    /// <summary>
    /// The prompt supplied to a runner.
    /// </summary>
    Prompt = 0,

    /// <summary>
    /// A persisted execution plan.
    /// </summary>
    Plan = 1,

    /// <summary>
    /// Captured runner standard output.
    /// </summary>
    StdoutLog = 2,

    /// <summary>
    /// Captured runner standard error.
    /// </summary>
    StderrLog = 3,

    /// <summary>
    /// A runner-produced result summary.
    /// </summary>
    Result = 4,

    /// <summary>
    /// A captured source diff.
    /// </summary>
    Diff = 5,

    /// <summary>
    /// A review artifact.
    /// </summary>
    Review = 6,

    /// <summary>
    /// A persisted approval request.
    /// </summary>
    ApprovalRequest = 7,

    /// <summary>
    /// Captured test output.
    /// </summary>
    TestOutput = 8,

    /// <summary>
    /// Structured runtime metadata.
    /// </summary>
    Metadata = 9,
}

/// <summary>
/// Provides conversion helpers for <see cref="ArtifactType"/>.
/// </summary>
public static class ArtifactTypeExtensions
{
    /// <summary>
    /// Converts an artifact type into its stable storage representation.
    /// </summary>
    /// <param name="type">The artifact type to convert.</param>
    /// <returns>The lowercase storage value for the artifact type.</returns>
    public static string ToStorageValue(this ArtifactType type) => type switch
    {
        ArtifactType.Prompt => "prompt",
        ArtifactType.Plan => "plan",
        ArtifactType.StdoutLog => "stdout_log",
        ArtifactType.StderrLog => "stderr_log",
        ArtifactType.Result => "result",
        ArtifactType.Diff => "diff",
        ArtifactType.Review => "review",
        ArtifactType.ApprovalRequest => "approval_request",
        ArtifactType.TestOutput => "test_output",
        ArtifactType.Metadata => "metadata",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown artifact type."),
    };

    /// <summary>
    /// Parses a stable storage value into an artifact type.
    /// </summary>
    /// <param name="value">The persisted artifact type value.</param>
    /// <returns>The matching artifact type.</returns>
    public static ArtifactType FromStorageValue(string value) => value switch
    {
        "prompt" => ArtifactType.Prompt,
        "plan" => ArtifactType.Plan,
        "stdout_log" => ArtifactType.StdoutLog,
        "stderr_log" => ArtifactType.StderrLog,
        "result" => ArtifactType.Result,
        "diff" => ArtifactType.Diff,
        "review" => ArtifactType.Review,
        "approval_request" => ArtifactType.ApprovalRequest,
        "test_output" => ArtifactType.TestOutput,
        "metadata" => ArtifactType.Metadata,
        _ => throw new ArgumentException($"Unknown artifact type '{value}'.", nameof(value)),
    };
}
