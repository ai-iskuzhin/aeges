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
            Assert.Contains("--no-interactive", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, null);
        }
    }

    private static string CreateVariableName() =>
        "AEGES_TEST_TELEGRAM_TOKEN_" + Guid.NewGuid().ToString("N");
}
