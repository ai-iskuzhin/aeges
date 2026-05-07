namespace Aeges.Application.Configuration;

/// <summary>
/// Configures local Aeges configuration loading.
/// </summary>
/// <param name="ConfigPath">The configuration file path. The default runtime config path is used when omitted.</param>
/// <param name="OptionalConfigFile">A value indicating whether the configuration file is optional.</param>
/// <param name="EnvironmentVariablePrefix">The environment variable prefix to read.</param>
public sealed record AegesConfigurationLoaderOptions(
    string? ConfigPath = null,
    bool OptionalConfigFile = true,
    string EnvironmentVariablePrefix = "AEGES_");
