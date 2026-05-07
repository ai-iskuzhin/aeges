namespace Aeges.Application.Configuration;

/// <summary>
/// Represents runner configuration.
/// </summary>
public sealed class AegesRunnersConfiguration
{
    /// <summary>
    /// Gets or sets the default runner identifier.
    /// </summary>
    public string Default { get; set; } = "codex";

    /// <summary>
    /// Gets or sets Codex runner configuration.
    /// </summary>
    public AegesCodexRunnerConfiguration Codex { get; set; } = new();
}
