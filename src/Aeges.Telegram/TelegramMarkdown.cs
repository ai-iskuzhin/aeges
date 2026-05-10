using System.Text;

namespace Aeges.Telegram;

/// <summary>
/// Converts plain Aeges response text into Telegram MarkdownV2-safe text.
/// </summary>
public static class TelegramMarkdown
{
    private static readonly HashSet<char> ReservedCharacters = ['_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!'];

    /// <summary>
    /// Escapes response text for MarkdownV2 while preserving leading block quote markers.
    /// </summary>
    /// <param name="text">The response text.</param>
    /// <returns>MarkdownV2-safe text.</returns>
    public static string EscapeResponseText(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var builder = new StringBuilder(text.Length);

        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0)
            {
                builder.Append('\n');
            }

            var line = lines[index];

            if (line.StartsWith("> ", StringComparison.Ordinal))
            {
                builder.Append('>');
                builder.Append(Escape(line[1..]));
                continue;
            }

            builder.Append(Escape(line));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Formats text as a Markdown block quote before transport escaping.
    /// </summary>
    /// <param name="text">The text to quote.</param>
    /// <returns>Plain response text marked as a quote block.</returns>
    public static string Quote(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "> (none)";
        }

        return string.Join(
            '\n',
            text.Trim().Split('\n').Select(line => $"> {line.TrimEnd()}"));
    }

    private static string Escape(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (ReservedCharacters.Contains(character))
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
