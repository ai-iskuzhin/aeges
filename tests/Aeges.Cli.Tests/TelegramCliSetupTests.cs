using Aeges.Application.Configuration;

namespace Aeges.Cli.Tests;

public sealed class TelegramCliSetupTests
{
    [Fact]
    public async Task EnsureTokenAsync_uses_existing_environment_token()
    {
        var variableName = CreateVariableName();
        Environment.SetEnvironmentVariable(variableName, "existing-token");

        try
        {
            var output = new StringWriter();
            var result = await TelegramCliSetup.EnsureTokenAsync(
                new AegesTelegramConfiguration { BotTokenEnvironmentVariable = variableName },
                new StringReader("ignored-token"),
                output,
                allowPrompt: true,
                CancellationToken.None);

            Assert.True(result);
            Assert.Equal("existing-token", Environment.GetEnvironmentVariable(variableName));
            Assert.Equal(string.Empty, output.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, null);
        }
    }

    [Fact]
    public async Task EnsureTokenAsync_prompts_and_sets_token_for_current_process()
    {
        var variableName = CreateVariableName();

        try
        {
            var output = new StringWriter();
            var result = await TelegramCliSetup.EnsureTokenAsync(
                new AegesTelegramConfiguration { BotTokenEnvironmentVariable = variableName },
                new StringReader("entered-token\n"),
                output,
                allowPrompt: true,
                CancellationToken.None);

            Assert.True(result);
            Assert.Equal("entered-token", Environment.GetEnvironmentVariable(variableName));
            Assert.Contains("for this process only", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("Allowed chat IDs are empty", output.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("entered-token", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, null);
        }
    }

    [Fact]
    public async Task EnsureTokenAsync_uses_configured_token_file()
    {
        var variableName = CreateVariableName();
        var directory = CreateTemporaryDirectory();
        var tokenPath = Path.Combine(directory, "telegram-token");
        await File.WriteAllTextAsync(tokenPath, "file-token\n");

        try
        {
            var output = new StringWriter();
            var result = await TelegramCliSetup.EnsureTokenAsync(
                new AegesTelegramConfiguration
                {
                    BotTokenEnvironmentVariable = variableName,
                    BotTokenFilePath = tokenPath,
                },
                new StringReader("ignored-token"),
                output,
                allowPrompt: false,
                CancellationToken.None);

            Assert.True(result);
            Assert.Equal(string.Empty, output.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, null);
        }
    }

    [Fact]
    public async Task EnsureTokenAsync_fails_without_prompt_when_token_is_missing()
    {
        var variableName = CreateVariableName();

        try
        {
            var output = new StringWriter();
            var result = await TelegramCliSetup.EnsureTokenAsync(
                new AegesTelegramConfiguration { BotTokenEnvironmentVariable = variableName },
                new StringReader("entered-token\n"),
                output,
                allowPrompt: false,
                CancellationToken.None);

            Assert.False(result);
            Assert.Null(Environment.GetEnvironmentVariable(variableName));
            Assert.Contains(variableName, output.ToString(), StringComparison.Ordinal);
            Assert.Contains("aeges telegram setup", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, null);
        }
    }

    [Fact]
    public async Task RunWizardAsync_writes_configured_secret_file_without_echoing_token()
    {
        var configPath = Path.Combine(CreateTemporaryDirectory(), "config.json");
        var tokenPath = Path.Combine(CreateTemporaryDirectory(), "telegram-token");
        var output = new StringWriter();

        var result = await TelegramCliSetup.RunWizardAsync(
            new AegesConfiguration(),
            configPath,
            new StringReader($"""
            {tokenPath}
            entered-token
            1001,1002

            """),
            output,
            CancellationToken.None);

        Assert.Equal(configPath, result.ConfigPath);
        Assert.Equal("AEGES_TELEGRAM_BOT_TOKEN", result.BotTokenEnvironmentVariable);
        Assert.Equal(tokenPath, result.BotTokenFilePath);
        Assert.Equal([1001, 1002], result.AllowedChatIds);
        Assert.Equal("entered-token\n", await File.ReadAllTextAsync(tokenPath));
        Assert.DoesNotContain("entered-token", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Telegram bot token environment variable [", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunWizardAsync_allows_open_local_chat_mode()
    {
        var configPath = Path.Combine(CreateTemporaryDirectory(), "config.json");
        var tokenPath = Path.Combine(CreateTemporaryDirectory(), "telegram-token");
        var output = new StringWriter();

        var result = await TelegramCliSetup.RunWizardAsync(
            new AegesConfiguration(),
            configPath,
            new StringReader($"{tokenPath}\nentered-token\nempty\n"),
            output,
            CancellationToken.None);

        Assert.Equal("AEGES_TELEGRAM_BOT_TOKEN", result.BotTokenEnvironmentVariable);
        Assert.Equal(tokenPath, result.BotTokenFilePath);
        Assert.Empty(result.AllowedChatIds);
        Assert.Contains("open to any chat", output.ToString(), StringComparison.Ordinal);
    }

    private static string CreateVariableName() =>
        "AEGES_TEST_TELEGRAM_TOKEN_" + Guid.NewGuid().ToString("N");

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "aeges-cli-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        return directory;
    }
}
