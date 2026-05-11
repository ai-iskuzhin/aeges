using Aeges.Core;

namespace Aeges.Storage;

/// <summary>
/// Provides persistence operations for governed discussion messages.
/// </summary>
public interface ITalkMessageRepository
{
    /// <summary>
    /// Adds a talk message to storage.
    /// </summary>
    /// <param name="message">The message to add.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(RuntimeTalkMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Lists messages belonging to a talk session.
    /// </summary>
    /// <param name="sessionId">The talk session identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The session messages in chronological order.</returns>
    Task<IReadOnlyList<RuntimeTalkMessage>> ListBySessionAsync(
        TalkSessionId sessionId,
        CancellationToken cancellationToken);
}
