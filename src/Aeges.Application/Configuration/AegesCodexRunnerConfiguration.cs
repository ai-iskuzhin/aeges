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
    /// Gets or sets the default Codex execution timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 1800;
}
