using Aeges.Core;

namespace Aeges.Storage;

/// <summary>
/// Provides persistence operations for governed discussion sessions.
/// </summary>
public interface ITalkSessionRepository
{
    /// <summary>
    /// Adds a talk session to storage.
    /// </summary>
    /// <param name="session">The session to add.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(RuntimeTalkSession session, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a talk session by identifier.
    /// </summary>
    /// <param name="id">The talk session identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching session, or <see langword="null"/> when none exists.</returns>
    Task<RuntimeTalkSession?> GetByIdAsync(TalkSessionId id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the most recently updated open talk session for a source.
    /// </summary>
    /// <param name="source">The stable source that owns session continuity.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The latest open session, or <see langword="null"/> when none exists.</returns>
    Task<RuntimeTalkSession?> GetLatestOpenBySourceAsync(string source, CancellationToken cancellationToken);

    /// <summary>
    /// Lists recently updated talk sessions.
    /// </summary>
    /// <param name="limit">The maximum number of sessions to return.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The recent sessions.</returns>
    Task<IReadOnlyList<RuntimeTalkSession>> ListRecentAsync(int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Updates a talk session in storage.
    /// </summary>
    /// <param name="session">The session to update.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(RuntimeTalkSession session, CancellationToken cancellationToken);
}
