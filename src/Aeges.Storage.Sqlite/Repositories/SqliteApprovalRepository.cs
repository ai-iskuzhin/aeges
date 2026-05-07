using Aeges.Core;
using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aeges.Storage.Sqlite.Repositories;

/// <summary>
/// EF Core SQLite implementation of <see cref="IApprovalRepository"/>.
/// </summary>
public sealed class SqliteApprovalRepository : IApprovalRepository
{
    private readonly AegesDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteApprovalRepository"/> class.
    /// </summary>
    /// <param name="context">The Aeges SQLite database context.</param>
    public SqliteApprovalRepository(AegesDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(ApprovalRequest approval, CancellationToken cancellationToken)
    {
        await context.Approvals.AddAsync(ToRecord(approval), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApprovalRequest?> GetByIdAsync(ApprovalId id, CancellationToken cancellationToken)
    {
        var record = await context.Approvals
            .AsNoTracking()
            .SingleOrDefaultAsync(approval => approval.Id == id.Value, cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApprovalRequest>> ListByTaskAsync(
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var records = await context.Approvals
            .AsNoTracking()
            .Where(approval => approval.TaskId == taskId.Value)
            .ToListAsync(cancellationToken);

        return records
            .OrderBy(approval => approval.CreatedAt)
            .ThenBy(approval => approval.Id)
            .Select(ToDomain)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApprovalRequest>> ListPendingAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be greater than zero.");
        }

        var pendingStatus = ApprovalStatus.Pending.ToStorageValue();
        var records = await context.Approvals
            .AsNoTracking()
            .Where(approval => approval.Status == pendingStatus)
            .ToListAsync(cancellationToken);

        return records
            .OrderBy(approval => approval.CreatedAt)
            .ThenBy(approval => approval.Id)
            .Take(limit)
            .Select(ToDomain)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(ApprovalRequest approval, CancellationToken cancellationToken)
    {
        var record = await context.Approvals
            .SingleOrDefaultAsync(existingApproval => existingApproval.Id == approval.Id.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Approval request '{approval.Id}' was not found.");

        record.Status = approval.Status.ToStorageValue();
        record.ResolvedAt = approval.ResolvedAt;
        record.ResolvedBy = approval.ResolvedBy;
    }

    private static ApprovalRecord ToRecord(ApprovalRequest approval) =>
        new()
        {
            Id = approval.Id.Value,
            TaskId = approval.TaskId.Value,
            IterationId = approval.IterationId?.Value,
            Status = approval.Status.ToStorageValue(),
            Reason = approval.Reason,
            RequestedAction = approval.RequestedAction,
            CreatedAt = approval.CreatedAt,
            ResolvedAt = approval.ResolvedAt,
            ResolvedBy = approval.ResolvedBy,
        };

    private static ApprovalRequest ToDomain(ApprovalRecord record) =>
        ApprovalRequest.Rehydrate(
            new ApprovalId(record.Id),
            new TaskId(record.TaskId),
            record.IterationId is null ? null : new IterationId(record.IterationId),
            ApprovalStatusExtensions.FromStorageValue(record.Status),
            record.Reason,
            record.RequestedAction,
            record.CreatedAt,
            record.ResolvedAt,
            record.ResolvedBy);
}
