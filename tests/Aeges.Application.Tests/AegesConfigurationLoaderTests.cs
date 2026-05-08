using Aeges.Application.Configuration;

namespace Aeges.Application.Tests;

public sealed class AegesConfigurationLoaderTests
{
    [Fact]
    public void Load_returns_defaults_when_optional_file_is_missing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "config.json");

        var configuration = new AegesConfigurationLoader().Load(
            new AegesConfigurationLoaderOptions(path));

        Assert.Equal("local", configuration.MachineId);
        Assert.Equal("sqlite", configuration.Storage.Provider);
        Assert.Equal("codex", configuration.Runners.Default);
        Assert.Equal("codex", configuration.Runners.Codex.Executable);
        Assert.Null(configuration.Runners.Codex.Model);
        Assert.Null(configuration.Runners.Codex.ReasoningEffort);
        Assert.Equal(1800, configuration.Runners.Codex.TimeoutSeconds);
        Assert.Empty(configuration.Projects);
    }

    [Fact]
    public void Load_binds_json_configuration()
    {
        var path = CreateConfigFile(
            """
            {
              "machineId": "home-laptop",
              "storage": {
                "provider": "sqlite",
                "connectionString": "Data Source=/tmp/aeges.db"
              },
              "telegram": {
                "botTokenEnvironmentVariable": "AEGES_TEST_TELEGRAM_TOKEN",
                "allowedChatIds": [1001, 1002]
              },
              "runners": {
                "default": "codex",
                "codex": {
                  "executable": "codex-test",
                  "model": "gpt-5.5",
                  "reasoningEffort": "high",
                  "timeoutSeconds": 120
                }
              },
              "projects": [
                {
                  "id": "aeges",
                  "name": "Aeges",
                  "path": "/work/aeges"
                }
              ]
            }
            """);

        var configuration = new AegesConfigurationLoader().Load(
            new AegesConfigurationLoaderOptions(path));

        Assert.Equal("home-laptop", configuration.MachineId);
        Assert.Equal("sqlite", configuration.Storage.Provider);
        Assert.Equal("Data Source=/tmp/aeges.db", configuration.Storage.ConnectionString);
        Assert.Equal("AEGES_TEST_TELEGRAM_TOKEN", configuration.Telegram.BotTokenEnvironmentVariable);
        Assert.Equal([1001, 1002], configuration.Telegram.AllowedChatIds);
        Assert.Equal("codex-test", configuration.Runners.Codex.Executable);
        Assert.Equal("gpt-5.5", configuration.Runners.Codex.Model);
        Assert.Equal("high", configuration.Runners.Codex.ReasoningEffort);
        Assert.Equal(120, configuration.Runners.Codex.TimeoutSeconds);
        Assert.Single(configuration.Projects);
        Assert.Equal("aeges", configuration.Projects[0].Id);
    }

    [Fact]
    public void Load_applies_command_line_overrides_after_file_configuration()
    {
        var path = CreateConfigFile(
            """
            {
              "machineId": "file-machine",
              "runners": {
                "codex": {
                  "timeoutSeconds": 120
                }
              }
            }
            """);

        var configuration = new AegesConfigurationLoader().Load(
            new AegesConfigurationLoaderOptions(path),
            [
                "--machineId=cli-machine",
                "--runners:codex:timeoutSeconds=240",
            ]);

        Assert.Equal("cli-machine", configuration.MachineId);
        Assert.Equal(240, configuration.Runners.Codex.TimeoutSeconds);
    }

    [Fact]
    public void Load_applies_environment_overrides()
    {
        var prefix = $"AEGES_TEST_{Guid.NewGuid():N}_";
        var machineKey = prefix + "machineId";
        var executableKey = prefix + "runners__codex__executable";
        Environment.SetEnvironmentVariable(machineKey, "env-machine");
        Environment.SetEnvironmentVariable(executableKey, "codex-env");

        try
        {
            var configuration = new AegesConfigurationLoader().Load(
                new AegesConfigurationLoaderOptions(
                    ConfigPath: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "config.json"),
                    EnvironmentVariablePrefix: prefix));

            Assert.Equal("env-machine", configuration.MachineId);
            Assert.Equal("codex-env", configuration.Runners.Codex.Executable);
        }
        finally
        {
            Environment.SetEnvironmentVariable(machineKey, null);
            Environment.SetEnvironmentVariable(executableKey, null);
        }
    }

    [Fact]
    public void Load_rejects_relative_config_path()
    {
        Assert.Throws<ArgumentException>(
            () => new AegesConfigurationLoader().Load(
                new AegesConfigurationLoaderOptions("relative/config.json")));
    }

    [Fact]
    public void Load_rejects_missing_required_file_when_not_optional()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "config.json");

        Assert.Throws<FileNotFoundException>(
            () => new AegesConfigurationLoader().Load(
                new AegesConfigurationLoaderOptions(path, OptionalConfigFile: false)));
    }

    [Fact]
    public void Load_rejects_invalid_timeout()
    {
        var path = CreateConfigFile(
            """
            {
              "runners": {
                "codex": {
                  "timeoutSeconds": 0
                }
              }
            }
            """);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AegesConfigurationLoader().Load(new AegesConfigurationLoaderOptions(path)));
    }

    [Fact]
    public void Load_rejects_empty_codex_model_settings()
    {
        var path = CreateConfigFile(
            """
            {
              "runners": {
                "codex": {
                  "model": " "
                }
              }
            }
            """);

        Assert.Throws<ArgumentException>(
            () => new AegesConfigurationLoader().Load(new AegesConfigurationLoaderOptions(path)));
    }

    private static string CreateConfigFile(string json)
    {
        var directory = Path.Combine(Path.GetTempPath(), "aeges-config-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "config.json");
        File.WriteAllText(path, json);

        return path;
    }
}
