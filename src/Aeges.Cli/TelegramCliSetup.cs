using System.Text;
using Aeges.Application.Configuration;

internal static class TelegramCliSetup
{
    public static async Task<bool> EnsureTokenAsync(
        AegesTelegramConfiguration configuration,
        TextReader input,
        TextWriter output,
        bool allowPrompt,
        CancellationToken cancellationToken)
    {
        var variableName = configuration.BotTokenEnvironmentVariable;

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variableName)))
        {
            return true;
        }

        if (!allowPrompt)
        {
            await output.WriteLineAsync(
                $"Telegram bot token environment variable '{variableName}' is not set. Set it or run without --json/--no-interactive to enter it.");
            return false;
        }

        await output.WriteLineAsync($"Telegram bot token environment variable '{variableName}' is not set.");
        await output.WriteAsync("Enter Telegram bot token for this run: ");

        var token = await ReadTokenAsync(input, cancellationToken);
        await output.WriteLineAsync();

        if (string.IsNullOrWhiteSpace(token))
        {
            await output.WriteLineAsync(
                $"Telegram setup cancelled. Set '{variableName}' or run 'aeges telegram run' again and enter a token.");
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

    private static async Task<string?> ReadTokenAsync(
        TextReader input,
        CancellationToken cancellationToken)
    {
        if (ReferenceEquals(input, Console.In) && !Console.IsInputRedirected)
        {
            return ReadHiddenConsoleLine();
        }

        return await input.ReadLineAsync(cancellationToken);
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
