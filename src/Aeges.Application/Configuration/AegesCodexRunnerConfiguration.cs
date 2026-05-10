namespace Aeges.Application.Configuration;

/// <summary>
/// Represents Codex CLI runner configuration.
/// </summary>
public sealed class AegesCodexRunnerConfiguration
{
    /// <summary>
    /// Gets or sets the Codex executable name or path.
    /// </summary>
    public string Executable { get; set; } = "codex";

    /// <summary>
    /// Gets or sets the Codex model identifier.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets the Codex model reasoning effort.
    /// </summary>
    public string? ReasoningEffort { get; set; }

    /// <summary>
    /// Gets or sets the Codex sandbox mode used for worker commands.
    /// </summary>
    public string SandboxMode { get; set; } = "workspace-write";

    /// <summary>
    /// Gets or sets a value indicating whether Codex should bypass its own approvals and sandbox.
    /// </summary>
    public bool BypassApprovalsAndSandbox { get; set; }

    /// <summary>
    /// Gets or sets the default Codex execution timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 1800;
}
