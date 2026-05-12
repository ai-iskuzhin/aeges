using Aeges.Core;
using Aeges.Storage;

namespace Aeges.Application.TelegramTaskBindings;

/// <summary>
/// Coordinates durable Telegram task routing bindings.
/// </summary>
public sealed class TelegramTaskBindingService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramTaskBindingService"/> class.
    /// </summary>
    /// <param name="unitOfWork">The persistence unit of work.</param>
    /// <param name="clock">The runtime clock.</param>
    public TelegramTaskBindingService(IUnitOfWork unitOfWork, IClock clock)
    {
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    /// <summary>
    /// Records where Telegram should route updates for a task.
    /// </summary>
    /// <param name="chatId">The Telegram chat identifier.</param>
    /// <param name="messageThreadId">The Telegram forum topic identifier, when available.</param>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="detailMessageId">The latest editable task details message, when available.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RecordAsync(
        long chatId,
        int? messageThreadId,
        TaskId taskId,
        int? detailMessageId,
        CancellationToken cancellationToken)
    {
        var now = clock.Now;
        var binding = RuntimeTelegramTaskBinding.Create(
            chatId,
            messageThreadId,
            taskId,
            detailMessageId,
            now);

        await unitOfWork.TelegramTaskBindings.UpsertAsync(binding, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Lists durable Telegram task bindings.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The durable Telegram task bindings.</returns>
    public async Task<IReadOnlyList<RuntimeTelegramTaskBinding>> ListAsync(CancellationToken cancellationToken) =>
        await unitOfWork.TelegramTaskBindings.ListAsync(cancellationToken);

    /// <summary>
    /// Forgets one Telegram task binding.
    /// </summary>
    /// <param name="chatId">The Telegram chat identifier.</param>
    /// <param name="messageThreadId">The Telegram forum topic identifier, when available.</param>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ForgetAsync(
        long chatId,
        int? messageThreadId,
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        await unitOfWork.TelegramTaskBindings.DeleteAsync(chatId, messageThreadId, taskId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
