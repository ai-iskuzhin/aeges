using System.Text.Json;
using Aeges.Runners;

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

    /// <summary>
    /// Attempts to turn a Codex JSONL event into a short runner progress event.
    /// </summary>
    /// <param name="jsonLine">One JSONL event line emitted by Codex.</param>
    /// <param name="progressEvent">The parsed progress event, when the line is user-visible progress.</param>
    /// <returns><see langword="true"/> when the line produced a progress event; otherwise <see langword="false"/>.</returns>
    public static bool TryCreateProgressEvent(string jsonLine, out RunnerProgressEvent? progressEvent)
    {
        progressEvent = null;

        if (string.IsNullOrWhiteSpace(jsonLine))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(jsonLine);
            var root = document.RootElement;

            if (!root.TryGetProperty("type", out var typeElement))
            {
                return false;
            }

            var type = typeElement.GetString();

            if (type == "thread.started")
            {
                var payloadJson = root.TryGetProperty("thread_id", out var threadIdElement)
                    ? JsonSerializer.Serialize(new { threadId = threadIdElement.GetString() })
                    : null;
                progressEvent = new RunnerProgressEvent("runner.session.started", "Codex session started.", payloadJson);

                return true;
            }

            if (type == "item.completed"
                && root.TryGetProperty("item", out var item)
                && item.TryGetProperty("type", out var itemTypeElement))
            {
                var itemType = itemTypeElement.GetString();

                if (itemType == "agent_message"
                    && item.TryGetProperty("text", out var textElement))
                {
                    var text = textElement.GetString();

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        progressEvent = new RunnerProgressEvent(
                            "runner.message",
                            Truncate(text.Trim(), 1_000),
                            JsonSerializer.Serialize(new { codexEventType = type, itemType }));

                        return true;
                    }
                }

                if (!string.IsNullOrWhiteSpace(itemType))
                {
                    progressEvent = new RunnerProgressEvent(
                        "runner.item.completed",
                        $"Codex completed {itemType}.",
                        JsonSerializer.Serialize(new { codexEventType = type, itemType }));

                    return true;
                }
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength
            ? value
            : string.Concat(value.AsSpan(0, maxLength - 3), "...");
}
