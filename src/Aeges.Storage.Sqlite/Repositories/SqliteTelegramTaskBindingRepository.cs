using Aeges.Core;
using Aeges.Storage;
using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite.Repositories;

/// <summary>
/// EF Core SQLite implementation of <see cref="ITelegramTaskBindingRepository"/>.
/// </summary>
public sealed class SqliteTelegramTaskBindingRepository : ITelegramTaskBindingRepository
{
    private readonly AegesDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteTelegramTaskBindingRepository"/> class.
    /// </summary>
    /// <param name="context">The Aeges SQLite database context.</param>
    public SqliteTelegramTaskBindingRepository(AegesDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc />
    public async Task UpsertAsync(RuntimeTelegramTaskBinding binding, CancellationToken cancellationToken)
    {
        var storedThreadId = ToStoredThreadId(binding.MessageThreadId);
        var record = await context.TelegramTaskBindings.SingleOrDefaultAsync(
            existing => existing.ChatId == binding.ChatId
                && existing.MessageThreadId == storedThreadId
                && existing.TaskId == binding.TaskId.Value,
            cancellationToken);

        if (record is null)
        {
            await context.TelegramTaskBindings.AddAsync(ToRecord(binding), cancellationToken);
            return;
        }

        record.DetailMessageId = binding.DetailMessageId;
        record.UpdatedAt = binding.UpdatedAt;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeTelegramTaskBinding>> ListAsync(CancellationToken cancellationToken)
    {
        var records = await context.TelegramTaskBindings
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        return
        [
            .. records
                .OrderByDescending(binding => binding.UpdatedAt)
                .Select(binding => RuntimeTelegramTaskBinding.Rehydrate(
                binding.ChatId,
                FromStoredThreadId(binding.MessageThreadId),
                new TaskId(binding.TaskId),
                binding.DetailMessageId,
                binding.CreatedAt,
                binding.UpdatedAt)),
        ];
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        long chatId,
        int? messageThreadId,
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var storedThreadId = ToStoredThreadId(messageThreadId);
        var record = await context.TelegramTaskBindings.SingleOrDefaultAsync(
            binding => binding.ChatId == chatId
                && binding.MessageThreadId == storedThreadId
                && binding.TaskId == taskId.Value,
            cancellationToken);

        if (record is not null)
        {
            context.TelegramTaskBindings.Remove(record);
        }
    }

    private static TelegramTaskBindingRecord ToRecord(RuntimeTelegramTaskBinding binding) =>
        new()
        {
            ChatId = binding.ChatId,
            MessageThreadId = ToStoredThreadId(binding.MessageThreadId),
            TaskId = binding.TaskId.Value,
            DetailMessageId = binding.DetailMessageId,
            CreatedAt = binding.CreatedAt,
            UpdatedAt = binding.UpdatedAt,
        };

    private static int ToStoredThreadId(int? messageThreadId) => messageThreadId ?? 0;

    private static int? FromStoredThreadId(int messageThreadId) => messageThreadId == 0 ? null : messageThreadId;
}
