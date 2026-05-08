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
    /// Gets or sets the default Codex execution timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 1800;
}
