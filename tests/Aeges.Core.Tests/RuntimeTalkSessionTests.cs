using Aeges.Core;

namespace Aeges.Core.Tests;

public sealed class RuntimeTalkSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 11, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Create_initializes_open_session()
    {
        var session = RuntimeTalkSession.Create(
            new TalkSessionId("talk-001"),
            "telegram:1001",
            "Setup",
            new RunnerId("codex"),
            Now);

        Assert.Equal(new TalkSessionId("talk-001"), session.Id);
        Assert.Equal("telegram:1001", session.Source);
        Assert.Equal("Setup", session.Title);
        Assert.Equal(new RunnerId("codex"), session.RunnerId);
        Assert.Equal(TalkSessionStatus.Open, session.Status);
        Assert.Null(session.ExternalSessionId);
        Assert.Equal(Now, session.CreatedAt);
        Assert.Equal(Now, session.UpdatedAt);
    }

    [Fact]
    public void RecordExternalSessionId_updates_session_continuity()
    {
        var session = RuntimeTalkSession.Create(
            new TalkSessionId("talk-001"),
            "cli",
            "Setup",
            new RunnerId("codex"),
            Now);

        session.RecordExternalSessionId("codex-thread-001", Now.AddMinutes(1));

        Assert.Equal("codex-thread-001", session.ExternalSessionId);
        Assert.Equal(Now.AddMinutes(1), session.UpdatedAt);
    }

    [Fact]
    public void Archive_closes_session()
    {
        var session = RuntimeTalkSession.Create(
            new TalkSessionId("talk-001"),
            "cli",
            "Setup",
            new RunnerId("codex"),
            Now);

        session.Archive(Now.AddMinutes(2));

        Assert.Equal(TalkSessionStatus.Archived, session.Status);
        Assert.Equal(Now.AddMinutes(2), session.ArchivedAt);
        Assert.Throws<AegesDomainException>(() => session.Touch(Now.AddMinutes(3)));
    }
}
