using Aeges.Telegram;

namespace Aeges.Telegram.Tests;

public sealed class TelegramMarkdownTests
{
    [Fact]
    public void EscapeResponseText_escapes_markdown_v2_reserved_characters()
    {
        var text = "Task: task-001 [done]!";

        var result = TelegramMarkdown.EscapeResponseText(text);

        Assert.Equal("""Task: task\-001 \[done\]\!""", result);
    }

    [Fact]
    public void EscapeResponseText_preserves_block_quote_markers()
    {
        var text = "Runner response:\n> I couldn't update notes/status.txt.";

        var result = TelegramMarkdown.EscapeResponseText(text);

        Assert.Equal("Runner response:\n> I couldn't update notes/status\\.txt\\.", result);
    }
}
