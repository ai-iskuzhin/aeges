namespace Aeges.Application.Configuration;

/// <summary>
/// Represents a configured project registration.
/// </summary>
public sealed class AegesProjectConfiguration
{
    /// <summary>
    /// Gets or sets the project identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project root path.
    /// </summary>
    public string Path { get; set; } = string.Empty;
}
