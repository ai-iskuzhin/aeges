using Aeges.Core;
using Aeges.Storage.Sqlite.Repositories;

namespace Aeges.Storage.Sqlite.Tests;

public sealed class SqliteTalkRepositoryTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 05, 11, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task Session_repository_round_trips_talk_session()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteTalkSessionRepository(context);
        var session = RuntimeTalkSession.Create(
            new TalkSessionId("talk-001"),
            "telegram:1001",
            "Setup",
            new RunnerId("codex"),
            CreatedAt);
        session.RecordExternalSessionId("thread-001", CreatedAt.AddMinutes(1));

        await repository.AddAsync(session, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(session.Id, CancellationToken.None);
        var latest = await repository.GetLatestOpenBySourceAsync("telegram:1001", CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(session.Id, stored.Id);
        Assert.Equal("thread-001", stored.ExternalSessionId);
        Assert.Equal(session.Id, latest!.Id);
    }

    [Fact]
    public async Task Message_repository_lists_messages_by_session()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var sessions = new SqliteTalkSessionRepository(context);
        var messages = new SqliteTalkMessageRepository(context);
        var session = RuntimeTalkSession.Create(
            new TalkSessionId("talk-001"),
            "cli",
            "Setup",
            new RunnerId("codex"),
            CreatedAt);
        await sessions.AddAsync(session, CancellationToken.None);
        await messages.AddAsync(
            RuntimeTalkMessage.Create(
                new TalkMessageId("talk-message-001"),
                session.Id,
                TalkMessageRole.User,
                "Hello",
                CreatedAt),
            CancellationToken.None);
        await messages.AddAsync(
            RuntimeTalkMessage.Create(
                new TalkMessageId("talk-message-002"),
                session.Id,
                TalkMessageRole.Assistant,
                "Hi",
                CreatedAt.AddSeconds(1)),
            CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await messages.ListBySessionAsync(session.Id, CancellationToken.None);

        Assert.Collection(
            stored,
            message => Assert.Equal(TalkMessageRole.User, message.Role),
            message => Assert.Equal(TalkMessageRole.Assistant, message.Role));
    }
}
