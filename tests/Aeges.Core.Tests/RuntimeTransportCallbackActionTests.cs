using Aeges.Core;

namespace Aeges.Core.Tests;

public sealed class RuntimeTransportCallbackActionTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 11, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Create_initializes_callback_action()
    {
        var action = RuntimeTransportCallbackAction.Create(
            "abc123",
            "telegram",
            "chat:1001",
            "callback",
            """{"callbackData":"ae:t"}""",
            Now,
            Now.AddDays(1));

        Assert.Equal("abc123", action.Token);
        Assert.Equal("telegram", action.Transport);
        Assert.Equal("chat:1001", action.Scope);
        Assert.False(action.IsExpired(Now.AddHours(1)));
        Assert.True(action.IsExpired(Now.AddDays(1)));
    }

    [Fact]
    public void MarkUsed_updates_usage_metadata()
    {
        var action = RuntimeTransportCallbackAction.Create(
            "abc123",
            "telegram",
            "chat:1001",
            "callback",
            """{"callbackData":"ae:t"}""",
            Now,
            Now.AddDays(1));

        action.MarkUsed(Now.AddMinutes(1));

        Assert.Equal(Now.AddMinutes(1), action.LastUsedAt);
        Assert.Equal(1, action.UseCount);
    }

    [Fact]
    public void Create_rejects_invalid_expiration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RuntimeTransportCallbackAction.Create(
                "abc123",
                "telegram",
                "chat:1001",
                "callback",
                "{}",
                Now,
                Now));
    }

    [Fact]
    public void Rehydrate_rejects_negative_use_count()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RuntimeTransportCallbackAction.Rehydrate(
                "abc123",
                "telegram",
                "chat:1001",
                "callback",
                "{}",
                Now,
                Now.AddDays(1),
                null,
                -1));
    }
}
