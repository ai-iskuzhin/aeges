namespace Aeges.Application.Configuration;

/// <summary>
/// Represents storage provider configuration.
/// </summary>
public sealed class AegesStorageConfiguration
{
    /// <summary>
    /// Gets or sets the storage provider name.
    /// </summary>
    public string Provider { get; set; } = "sqlite";

    /// <summary>
    /// Gets or sets the storage connection string.
    /// </summary>
    public string? ConnectionString { get; set; }
}
