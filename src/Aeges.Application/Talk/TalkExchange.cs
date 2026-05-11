using Aeges.Core;

namespace Aeges.Application.Talk;

/// <summary>
/// Represents one completed discussion exchange.
/// </summary>
/// <param name="Session">The discussion session.</param>
/// <param name="UserMessage">The stored user message.</param>
/// <param name="AssistantMessage">The stored assistant response.</param>
public sealed record TalkExchange(
    RuntimeTalkSession Session,
    RuntimeTalkMessage UserMessage,
    RuntimeTalkMessage AssistantMessage);
