using Aeges.Core;
using Aeges.Storage;

namespace Aeges.Application.Transports;

/// <summary>
/// Coordinates durable transport callback action storage for UI transports.
/// </summary>
public sealed class TransportCallbackActionService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransportCallbackActionService"/> class.
    /// </summary>
    /// <param name="unitOfWork">The persistence unit of work.</param>
    /// <param name="clock">The deterministic application clock.</param>
    public TransportCallbackActionService(IUnitOfWork unitOfWork, IClock clock)
    {
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    /// <summary>
    /// Attempts to persist a callback action when its token is not already used.
    /// </summary>
    /// <param name="action">The callback action to persist.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the action was persisted.</returns>
    public async Task<bool> TryRegisterAsync(
        RuntimeTransportCallbackAction action,
        CancellationToken cancellationToken)
    {
        var existing = await unitOfWork.TransportCallbackActions.GetAsync(
            action.Transport,
            action.Token,
            cancellationToken);

        if (existing is not null)
        {
            return false;
        }

        await unitOfWork.TransportCallbackActions.AddAsync(action, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Resolves a callback action for a specific transport scope and records the use.
    /// </summary>
    /// <param name="transport">The owning transport.</param>
    /// <param name="scope">The transport-specific scope where the token is valid.</param>
    /// <param name="token">The short callback token.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The resolved action, or <see langword="null"/> when the token is invalid.</returns>
    public async Task<RuntimeTransportCallbackAction?> ResolveAsync(
        string transport,
        string scope,
        string token,
        CancellationToken cancellationToken)
    {
        var action = await unitOfWork.TransportCallbackActions.GetAsync(transport, token, cancellationToken);

        if (action is null || action.Scope != scope || action.IsExpired(clock.Now))
        {
            return null;
        }

        action.MarkUsed(clock.Now);
        await unitOfWork.TransportCallbackActions.UpdateAsync(action, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return action;
    }

    /// <summary>
    /// Deletes expired callback actions from durable storage.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The number of deleted actions.</returns>
    public async Task<int> PruneExpiredAsync(CancellationToken cancellationToken)
    {
        var removed = await unitOfWork.TransportCallbackActions.PruneExpiredAsync(clock.Now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return removed;
    }
}
