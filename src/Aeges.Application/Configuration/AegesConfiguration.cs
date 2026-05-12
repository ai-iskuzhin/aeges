namespace Aeges.Application.Configuration;

/// <summary>
/// Represents local Aeges runtime configuration.
/// </summary>
public sealed class AegesConfiguration
{
    /// <summary>
    /// Gets or sets the local machine identifier.
    /// </summary>
    public string MachineId { get; set; } = "local";

    /// <summary>
    /// Gets or sets storage configuration.
    /// </summary>
    public AegesStorageConfiguration Storage { get; set; } = new();

    /// <summary>
    /// Gets or sets Telegram transport configuration.
    /// </summary>
    public AegesTelegramConfiguration Telegram { get; set; } = new();

    /// <summary>
    /// Gets or sets local agent runtime configuration.
    /// </summary>
    public AegesAgentConfiguration Agent { get; set; } = new();

    /// <summary>
    /// Gets or sets runner configuration.
    /// </summary>
    public AegesRunnersConfiguration Runners { get; set; } = new();

    /// <summary>
    /// Gets or sets registered project configuration.
    /// </summary>
    public List<AegesProjectConfiguration> Projects { get; set; } = [];
}
