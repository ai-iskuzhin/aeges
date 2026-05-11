using Aeges.Core;
using Aeges.Runners;

namespace Aeges.Runners.Tests;

public sealed class TalkRunnerRequestTests
{
    [Fact]
    public void Constructor_initializes_request()
    {
        var request = new TalkRunnerRequest(
            new TalkSessionId("talk-001"),
            "Hello",
            "/tmp/aeges",
            "/tmp/aeges/artifacts/talk/talk-001",
            TimeSpan.FromSeconds(30),
            sessionPolicy: RunnerSessionPolicy.ResumeSession,
            externalSessionId: "thread-001");

        Assert.Equal(new TalkSessionId("talk-001"), request.SessionId);
        Assert.Equal("Hello", request.Prompt);
        Assert.Equal(RunnerSessionPolicy.ResumeSession, request.SessionPolicy);
        Assert.Equal("thread-001", request.ExternalSessionId);
    }

    [Fact]
    public void Constructor_rejects_invalid_session_policy_combination()
    {
        Assert.Throws<ArgumentException>(
            () => new TalkRunnerRequest(
                new TalkSessionId("talk-001"),
                "Hello",
                "/tmp/aeges",
                "/tmp/aeges/artifacts/talk/talk-001",
                TimeSpan.FromSeconds(30),
                sessionPolicy: RunnerSessionPolicy.ResumeSession));
    }
}
