using Aeges.Core;

namespace Aeges.Storage;

/// <summary>
/// Provides persistence operations for project discovery roots.
/// </summary>
public interface IProjectRootRepository
{
    /// <summary>
    /// Adds a project root to storage.
    /// </summary>
    /// <param name="root">The root to add.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(RuntimeProjectRoot root, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a project root by identifier.
    /// </summary>
    /// <param name="id">The project root identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching root, or <see langword="null"/> when none exists.</returns>
    Task<RuntimeProjectRoot?> GetByIdAsync(ProjectRootId id, CancellationToken cancellationToken);

    /// <summary>
    /// Lists registered project roots.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The registered project roots.</returns>
    Task<IReadOnlyList<RuntimeProjectRoot>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Updates a project root in storage.
    /// </summary>
    /// <param name="root">The root to update.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(RuntimeProjectRoot root, CancellationToken cancellationToken);
}
