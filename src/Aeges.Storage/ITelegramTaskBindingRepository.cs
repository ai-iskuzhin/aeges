using Aeges.Core;

namespace Aeges.Storage;

/// <summary>
/// Provides persistence operations for Telegram task routing bindings.
/// </summary>
public interface ITelegramTaskBindingRepository
{
    /// <summary>
    /// Adds or updates a Telegram task binding.
    /// </summary>
    /// <param name="binding">The binding to persist.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpsertAsync(RuntimeTelegramTaskBinding binding, CancellationToken cancellationToken);

    /// <summary>
    /// Lists durable Telegram task bindings.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The durable Telegram task bindings.</returns>
    Task<IReadOnlyList<RuntimeTelegramTaskBinding>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a Telegram task binding.
    /// </summary>
    /// <param name="chatId">The Telegram chat identifier.</param>
    /// <param name="messageThreadId">The Telegram forum topic identifier, when available.</param>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(
        long chatId,
        int? messageThreadId,
        TaskId taskId,
        CancellationToken cancellationToken);
}
