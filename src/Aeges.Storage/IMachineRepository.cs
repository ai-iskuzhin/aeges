using Aeges.Core;

namespace Aeges.Storage;

/// <summary>
/// Provides persistence operations for registered machines.
/// </summary>
public interface IMachineRepository
{
    /// <summary>
    /// Adds a machine to storage.
    /// </summary>
    /// <param name="machine">The machine to add.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(RuntimeMachine machine, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a machine by identifier.
    /// </summary>
    /// <param name="id">The machine identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching machine, or <see langword="null"/> when none exists.</returns>
    Task<RuntimeMachine?> GetByIdAsync(MachineId id, CancellationToken cancellationToken);

    /// <summary>
    /// Lists registered machines.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The registered machines.</returns>
    Task<IReadOnlyList<RuntimeMachine>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Updates a machine in storage.
    /// </summary>
    /// <param name="machine">The machine to update.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(RuntimeMachine machine, CancellationToken cancellationToken);
}
