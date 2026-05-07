using Aeges.Application.Runtime;
using Microsoft.Extensions.Configuration;

namespace Aeges.Application.Configuration;

/// <summary>
/// Loads local Aeges configuration from durable files, environment variables, and command-line arguments.
/// </summary>
public sealed class AegesConfigurationLoader
{
    /// <summary>
    /// Loads local Aeges configuration.
    /// </summary>
    /// <param name="options">The configuration loader options.</param>
    /// <param name="commandLineArguments">Command-line arguments to apply after file and environment sources.</param>
    /// <returns>The loaded configuration.</returns>
    public AegesConfiguration Load(
        AegesConfigurationLoaderOptions? options = null,
        IReadOnlyList<string>? commandLineArguments = null)
    {
        var effectiveOptions = options ?? new AegesConfigurationLoaderOptions();
        var configPath = ResolveConfigPath(effectiveOptions);

        var builder = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: effectiveOptions.OptionalConfigFile, reloadOnChange: false)
            .AddEnvironmentVariables(effectiveOptions.EnvironmentVariablePrefix);

        if (commandLineArguments is { Count: > 0 })
        {
            builder.AddCommandLine(commandLineArguments.ToArray());
        }

        var configuration = new AegesConfiguration();
        builder.Build().Bind(configuration);

        return Normalize(configuration);
    }

    private static string ResolveConfigPath(AegesConfigurationLoaderOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.EnvironmentVariablePrefix))
        {
            throw new ArgumentException("Environment variable prefix must not be empty.", nameof(options));
        }

        return options.ConfigPath is null
            ? RuntimeDirectoryLayout.CreateDefault().ConfigPath
            : RequireFullyQualifiedPath(options.ConfigPath);
    }

    private static string RequireFullyQualifiedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Configuration path must not be empty.", nameof(path));
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("Configuration path must be absolute.", nameof(path));
        }

        return Path.GetFullPath(path);
    }

    private static AegesConfiguration Normalize(AegesConfiguration configuration)
    {
        configuration.MachineId = RequireText(configuration.MachineId, nameof(configuration.MachineId));
        configuration.Storage ??= new AegesStorageConfiguration();
        configuration.Storage.Provider = RequireText(configuration.Storage.Provider, nameof(configuration.Storage.Provider));
        configuration.Telegram ??= new AegesTelegramConfiguration();
        configuration.Telegram.BotTokenEnvironmentVariable = RequireText(
            configuration.Telegram.BotTokenEnvironmentVariable,
            nameof(configuration.Telegram.BotTokenEnvironmentVariable));
        configuration.Telegram.AllowedChatIds ??= [];
        configuration.Runners ??= new AegesRunnersConfiguration();
        configuration.Runners.Default = RequireText(configuration.Runners.Default, nameof(configuration.Runners.Default));
        configuration.Runners.Codex ??= new AegesCodexRunnerConfiguration();
        configuration.Runners.Codex.Executable = RequireText(
            configuration.Runners.Codex.Executable,
            nameof(configuration.Runners.Codex.Executable));

        if (configuration.Runners.Codex.TimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration.Runners.Codex.TimeoutSeconds),
                configuration.Runners.Codex.TimeoutSeconds,
                "Codex timeout must be greater than zero.");
        }

        configuration.Projects ??= [];

        return configuration;
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Configuration value must not be empty.", parameterName);
        }

        return value;
    }
}
