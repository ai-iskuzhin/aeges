using Aeges.Core;

namespace Aeges.Storage;

/// <summary>
/// Provides persistence operations for short-token transport callback actions.
/// </summary>
public interface ITransportCallbackActionRepository
{
    /// <summary>
    /// Adds a transport callback action to storage.
    /// </summary>
    /// <param name="action">The action to add.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(RuntimeTransportCallbackAction action, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a transport callback action by transport and token.
    /// </summary>
    /// <param name="transport">The owning transport.</param>
    /// <param name="token">The short callback token.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching action, or <see langword="null"/> when none exists.</returns>
    Task<RuntimeTransportCallbackAction?> GetAsync(
        string transport,
        string token,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates a transport callback action in storage.
    /// </summary>
    /// <param name="action">The action to update.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(RuntimeTransportCallbackAction action, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes expired callback actions.
    /// </summary>
    /// <param name="now">The expiration cutoff.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The number of deleted rows.</returns>
    Task<int> PruneExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
