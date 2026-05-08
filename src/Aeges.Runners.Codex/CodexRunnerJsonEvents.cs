using System.Text.Json;

namespace Aeges.Runners.Codex;

/// <summary>
/// Parses machine-readable JSON events emitted by <c>codex exec --json</c>.
/// </summary>
public static class CodexRunnerJsonEvents
{
    /// <summary>
    /// Attempts to read a Codex thread identifier from a JSONL event.
    /// </summary>
    /// <param name="jsonLine">One JSONL event line emitted by Codex.</param>
    /// <param name="threadId">The parsed thread identifier, when present.</param>
    /// <returns><see langword="true"/> when the line contains a thread identifier; otherwise <see langword="false"/>.</returns>
    public static bool TryReadThreadId(string jsonLine, out string? threadId)
    {
        threadId = null;

        if (string.IsNullOrWhiteSpace(jsonLine))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(jsonLine);
            var root = document.RootElement;

            if (!root.TryGetProperty("type", out var type)
                || type.GetString() != "thread.started"
                || !root.TryGetProperty("thread_id", out var threadIdElement))
            {
                return false;
            }

            threadId = threadIdElement.GetString();

            return !string.IsNullOrWhiteSpace(threadId);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
