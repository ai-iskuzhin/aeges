using System.Text;
using Aeges.Application.Configuration;
using Aeges.Application.Runtime;

internal static class TelegramCliSetup
{
    private const string DefaultBotTokenEnvironmentVariable = "AEGES_TELEGRAM_BOT_TOKEN";

    public static async Task<bool> EnsureTokenAsync(
        AegesTelegramConfiguration configuration,
        TextReader input,
        TextWriter output,
        bool allowPrompt,
        CancellationToken cancellationToken)
    {
        var variableName = configuration.BotTokenEnvironmentVariable;

        if (HasConfiguredToken(configuration))
        {
            return true;
        }

        if (!allowPrompt)
        {
            await output.WriteLineAsync(
                $"Telegram bot token is not configured. Set '{variableName}', configure telegram.botTokenFilePath, or run 'aeges telegram setup'.");
            return false;
        }

        await output.WriteLineAsync($"Telegram bot token environment variable '{variableName}' is not set.");
        await output.WriteAsync("Enter Telegram bot token for this run: ");

        var token = await ReadSecretAsync(input, cancellationToken);
        await output.WriteLineAsync();

        if (string.IsNullOrWhiteSpace(token))
        {
            await output.WriteLineAsync(
                $"Telegram setup cancelled. Set '{variableName}', run 'aeges telegram setup', or run 'aeges telegram run' again and enter a token.");
            return false;
        }

        Environment.SetEnvironmentVariable(variableName, token);
        await output.WriteLineAsync($"Loaded Telegram bot token into '{variableName}' for this process only.");

        if (configuration.AllowedChatIds.Count == 0)
        {
            await output.WriteLineAsync(
                "Allowed chat IDs are empty; this run accepts messages from any chat. Configure telegram.allowedChatIds to restrict access.");
        }

        return true;
    }

    public static async Task<TelegramSetupResult> RunWizardAsync(
        AegesConfiguration configuration,
        string configPath,
        TextReader input,
        TextWriter output,
        CancellationToken cancellationToken,
        RuntimeDirectoryLayout? layout = null)
    {
        layout ??= RuntimeDirectoryLayout.CreateDefault();
        var defaultTokenFilePath = Path.Combine(layout.SecretsPath, "telegram-bot-token");

        await output.WriteLineAsync("Aeges Telegram setup");
        await output.WriteLineAsync($"Config: {configPath}");
        await output.WriteLineAsync();

        configuration.Telegram.BotTokenEnvironmentVariable = DefaultBotTokenEnvironmentVariable;
        var tokenFilePath = defaultTokenFilePath;

        await output.WriteLineAsync("Token source: local secret file.");
        await output.WriteLineAsync($"Token file: {tokenFilePath}");
        await output.WriteLineAsync($"Environment override: {DefaultBotTokenEnvironmentVariable}");

        var existingTokenFile = File.Exists(tokenFilePath);
        while (true)
        {
            await output.WriteAsync(existingTokenFile
                ? "Telegram bot token [press Enter to keep existing]: "
                : "Telegram bot token: ");

            var token = await ReadSecretAsync(input, cancellationToken);
            await output.WriteLineAsync();

            if (!string.IsNullOrWhiteSpace(token))
            {
                WriteSecretFile(tokenFilePath, token);
                await output.WriteLineAsync("Saved Telegram bot token to the local secret file.");
                break;
            }

            if (existingTokenFile)
            {
                await output.WriteLineAsync("Keeping existing local secret file.");
                break;
            }

            await output.WriteLineAsync("A token is required to create a new local secret file. Press Ctrl+C to cancel.");
        }

        configuration.Telegram.BotTokenFilePath = tokenFilePath;
        configuration.Telegram.AllowedChatIds = await PromptAllowedChatIdsAsync(
            input,
            output,
            configuration.Telegram.AllowedChatIds,
            cancellationToken);

        await output.WriteLineAsync();
        await output.WriteLineAsync("Telegram setup saved.");

        if (configuration.Telegram.AllowedChatIds.Count == 0)
        {
            await output.WriteLineAsync("Allowed chat IDs are empty; Telegram is open to any chat for local MVP setup.");
        }

        return new TelegramSetupResult(
            configPath,
            configuration.Telegram.BotTokenEnvironmentVariable,
            configuration.Telegram.BotTokenFilePath,
            configuration.Telegram.AllowedChatIds);
    }

    internal static bool HasConfiguredToken(AegesTelegramConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(configuration.BotTokenEnvironmentVariable)))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(configuration.BotTokenFilePath)
            && File.Exists(configuration.BotTokenFilePath)
            && !string.IsNullOrWhiteSpace(File.ReadAllText(configuration.BotTokenFilePath));
    }

    private static async Task<List<long>> PromptAllowedChatIdsAsync(
        TextReader input,
        TextWriter output,
        IReadOnlyList<long> defaultChatIds,
        CancellationToken cancellationToken)
    {
        var defaultText = defaultChatIds.Count == 0
            ? "empty"
            : string.Join(",", defaultChatIds);

        while (true)
        {
            await output.WriteAsync($"Allowed Telegram chat IDs, comma-separated [{defaultText}]: ");
            var value = await input.ReadLineAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(value))
            {
                return [.. defaultChatIds];
            }

            if (value.Trim().Equals("empty", StringComparison.OrdinalIgnoreCase))
            {
                return [];
            }

            if (TryParseChatIds(value, out var chatIds))
            {
                return chatIds;
            }

            await output.WriteLineAsync("Enter numeric chat IDs separated by commas, or `empty` for local open mode.");
        }
    }

    private static bool TryParseChatIds(string value, out List<long> chatIds)
    {
        chatIds = [];
        var parts = value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (!long.TryParse(part, out var chatId))
            {
                chatIds = [];
                return false;
            }

            chatIds.Add(chatId);
        }

        return true;
    }

    private static async Task<string?> ReadSecretAsync(
        TextReader input,
        CancellationToken cancellationToken)
    {
        if (ReferenceEquals(input, Console.In) && !Console.IsInputRedirected)
        {
            return ReadHiddenConsoleLine();
        }

        return await input.ReadLineAsync(cancellationToken);
    }

    private static void WriteSecretFile(string path, string secret)
    {
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            path,
            secret.Trim() + Environment.NewLine,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static string ReadHiddenConsoleLine()
    {
        var value = new StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                return value.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (value.Length > 0)
                {
                    value.Length--;
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                value.Append(key.KeyChar);
            }
        }
    }
}

internal sealed record TelegramSetupResult(
    string ConfigPath,
    string BotTokenEnvironmentVariable,
    string? BotTokenFilePath,
    IReadOnlyList<long> AllowedChatIds);
