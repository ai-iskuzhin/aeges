using Aeges.Core;

namespace Aeges.Core.Tests;

public sealed class RuntimeTalkMessageTests
{
    [Fact]
    public void Create_initializes_message()
    {
        var createdAt = new DateTimeOffset(2026, 05, 11, 12, 00, 00, TimeSpan.Zero);

        var message = RuntimeTalkMessage.Create(
            new TalkMessageId("talk-message-001"),
            new TalkSessionId("talk-001"),
            TalkMessageRole.User,
            "Hello",
            createdAt);

        Assert.Equal(new TalkMessageId("talk-message-001"), message.Id);
        Assert.Equal(new TalkSessionId("talk-001"), message.SessionId);
        Assert.Equal(TalkMessageRole.User, message.Role);
        Assert.Equal("Hello", message.Content);
        Assert.Equal(createdAt, message.CreatedAt);
    }
}
