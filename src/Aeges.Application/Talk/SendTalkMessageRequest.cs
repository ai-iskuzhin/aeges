using Aeges.Core;

namespace Aeges.Application.Talk;

/// <summary>
/// Describes a request to send one message into a governed discussion session.
/// </summary>
/// <param name="Source">The stable source that owns session continuity.</param>
/// <param name="Message">The human operator message.</param>
/// <param name="SessionId">The explicit session to continue, when supplied.</param>
/// <param name="StartNewSession">A value indicating whether a new session should be started even when one is open.</param>
/// <param name="Title">The optional title for a newly created session.</param>
public sealed record SendTalkMessageRequest(
    string Source,
    string Message,
    TalkSessionId? SessionId = null,
    bool StartNewSession = false,
    string? Title = null);
