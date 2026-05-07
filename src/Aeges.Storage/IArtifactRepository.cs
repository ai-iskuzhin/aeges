using Aeges.Core;

namespace Aeges.Storage;

/// <summary>
/// Provides persistence operations for artifact metadata.
/// </summary>
public interface IArtifactRepository
{
    /// <summary>
    /// Adds artifact metadata to storage.
    /// </summary>
    /// <param name="artifact">The artifact metadata to add.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(RuntimeArtifact artifact, CancellationToken cancellationToken);

    /// <summary>
    /// Gets artifact metadata by identifier.
    /// </summary>
    /// <param name="id">The artifact identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching artifact metadata, or <see langword="null"/> when none exists.</returns>
    Task<RuntimeArtifact?> GetByIdAsync(ArtifactId id, CancellationToken cancellationToken);

    /// <summary>
    /// Lists artifacts belonging to a task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task artifacts.</returns>
    Task<IReadOnlyList<RuntimeArtifact>> ListByTaskAsync(TaskId taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists artifacts belonging to an iteration.
    /// </summary>
    /// <param name="iterationId">The iteration identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The iteration artifacts.</returns>
    Task<IReadOnlyList<RuntimeArtifact>> ListByIterationAsync(
        IterationId iterationId,
        CancellationToken cancellationToken);
}
