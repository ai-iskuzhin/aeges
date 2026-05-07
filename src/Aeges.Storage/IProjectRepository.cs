using Aeges.Core;

namespace Aeges.Storage;

/// <summary>
/// Provides persistence operations for registered projects.
/// </summary>
public interface IProjectRepository
{
    /// <summary>
    /// Adds a project to storage.
    /// </summary>
    /// <param name="project">The project to add.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(RuntimeProject project, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a project by identifier.
    /// </summary>
    /// <param name="id">The project identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching project, or <see langword="null"/> when none exists.</returns>
    Task<RuntimeProject?> GetByIdAsync(ProjectId id, CancellationToken cancellationToken);

    /// <summary>
    /// Lists registered projects.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The registered projects.</returns>
    Task<IReadOnlyList<RuntimeProject>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Updates a project in storage.
    /// </summary>
    /// <param name="project">The project to update.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(RuntimeProject project, CancellationToken cancellationToken);
}
