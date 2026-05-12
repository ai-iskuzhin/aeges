using Aeges.Core;

namespace Aeges.Storage;

/// <summary>
/// Provides persistence operations for project groups.
/// </summary>
public interface IProjectGroupRepository
{
    /// <summary>
    /// Adds a project group to storage.
    /// </summary>
    /// <param name="group">The group to add.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(RuntimeProjectGroup group, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a project group by identifier.
    /// </summary>
    /// <param name="id">The project group identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching group, or <see langword="null"/> when none exists.</returns>
    Task<RuntimeProjectGroup?> GetByIdAsync(ProjectGroupId id, CancellationToken cancellationToken);

    /// <summary>
    /// Lists registered project groups.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The registered project groups.</returns>
    Task<IReadOnlyList<RuntimeProjectGroup>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Updates a project group in storage.
    /// </summary>
    /// <param name="group">The group to update.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(RuntimeProjectGroup group, CancellationToken cancellationToken);
}
