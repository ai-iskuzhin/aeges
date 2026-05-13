using Aeges.Runners.Codex;

namespace Aeges.Runners.Tests;

public sealed class CodexRunnerJsonEventsTests
{
    [Fact]
    public void TryCreateProgressEvent_reports_thread_started()
    {
        var created = CodexRunnerJsonEvents.TryCreateProgressEvent(
            """{"type":"thread.started","thread_id":"thread-001"}""",
            out var progressEvent);

        Assert.True(created);
        Assert.NotNull(progressEvent);
        Assert.Equal("runner.session.started", progressEvent.EventType);
        Assert.Equal("Codex session started.", progressEvent.Message);
        Assert.Contains("thread-001", progressEvent.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public void TryCreateProgressEvent_reports_agent_message_without_exposing_raw_non_message_events()
    {
        var created = CodexRunnerJsonEvents.TryCreateProgressEvent(
            """{"type":"item.completed","item":{"type":"agent_message","text":"Done with the first pass."}}""",
            out var progressEvent);

        Assert.True(created);
        Assert.NotNull(progressEvent);
        Assert.Equal("runner.message", progressEvent.EventType);
        Assert.Equal("Done with the first pass.", progressEvent.Message);
    }

    [Fact]
    public void TryCreateProgressEvent_ignores_invalid_json()
    {
        var created = CodexRunnerJsonEvents.TryCreateProgressEvent("not-json", out var progressEvent);

        Assert.False(created);
        Assert.Null(progressEvent);
    }
}
