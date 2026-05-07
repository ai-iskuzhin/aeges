using Aeges.Core;
using Aeges.Storage.Sqlite.Repositories;

namespace Aeges.Storage.Sqlite.Tests;

public sealed class SqliteApprovalRepositoryTests
{
    [Fact]
    public async Task Add_and_get_round_trips_approval()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SeedTaskAndIterationAsync(context);
        var repository = new SqliteApprovalRepository(context);
        var approval = CreateApproval(new ApprovalId("approval-001"));

        await repository.AddAsync(approval, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(new ApprovalId("approval-001"), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(approval.Id, stored.Id);
        Assert.Equal(approval.TaskId, stored.TaskId);
        Assert.Equal(approval.IterationId, stored.IterationId);
        Assert.Equal(approval.Status, stored.Status);
        Assert.Equal(approval.Reason, stored.Reason);
        Assert.Equal(approval.RequestedAction, stored.RequestedAction);
        Assert.Equal(approval.CreatedAt, stored.CreatedAt);
    }

    [Fact]
    public async Task ListByTask_and_ListPending_order_by_creation()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SeedTaskAndIterationAsync(context);
        var repository = new SqliteApprovalRepository(context);
        var rejected = CreateApproval(new ApprovalId("approval-rejected"), SqliteRepositorySeed.CreatedAt.AddMinutes(3));
        rejected.Reject("operator", SqliteRepositorySeed.CreatedAt.AddMinutes(4));

        await repository.AddAsync(CreateApproval(new ApprovalId("approval-002"), SqliteRepositorySeed.CreatedAt.AddMinutes(2)), CancellationToken.None);
        await repository.AddAsync(CreateApproval(new ApprovalId("approval-001"), SqliteRepositorySeed.CreatedAt.AddMinutes(1)), CancellationToken.None);
        await repository.AddAsync(rejected, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var byTask = await repository.ListByTaskAsync(new TaskId("task-001"), CancellationToken.None);
        var pending = await repository.ListPendingAsync(limit: 1, CancellationToken.None);

        Assert.Collection(
            byTask,
            approval => Assert.Equal(new ApprovalId("approval-001"), approval.Id),
            approval => Assert.Equal(new ApprovalId("approval-002"), approval.Id),
            approval => Assert.Equal(new ApprovalId("approval-rejected"), approval.Id));
        Assert.Single(pending);
        Assert.Equal(new ApprovalId("approval-001"), pending[0].Id);
    }

    [Fact]
    public async Task Update_persists_resolution()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await SeedTaskAndIterationAsync(context);
        var repository = new SqliteApprovalRepository(context);
        var approval = CreateApproval(new ApprovalId("approval-001"));
        await repository.AddAsync(approval, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var resolvedAt = SqliteRepositorySeed.CreatedAt.AddMinutes(5);
        approval.Approve("operator", resolvedAt);
        await repository.UpdateAsync(approval, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.GetByIdAsync(new ApprovalId("approval-001"), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(ApprovalStatus.Approved, stored.Status);
        Assert.Equal("operator", stored.ResolvedBy);
        Assert.Equal(resolvedAt, stored.ResolvedAt);
    }

    [Fact]
    public async Task ListPending_rejects_non_positive_limit()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new SqliteApprovalRepository(context);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => repository.ListPendingAsync(0, CancellationToken.None));
    }

    private static ApprovalRequest CreateApproval(ApprovalId id, DateTimeOffset? createdAt = null) =>
        ApprovalRequest.Create(
            id,
            new TaskId("task-001"),
            new IterationId("iteration-001"),
            "Dependency change requires approval.",
            "Add EF Core SQLite package.",
            createdAt ?? SqliteRepositorySeed.CreatedAt);

    private static async Task SeedTaskAndIterationAsync(AegesDbContext context)
    {
        await SqliteRepositorySeed.SeedProjectMachineAndTaskAsync(context);
        var iterations = new SqliteIterationRepository(context);
        await iterations.AddAsync(
            TaskIteration.Create(
                new IterationId("iteration-001"),
                new TaskId("task-001"),
                1,
                new RunnerId("codex"),
                SqliteRepositorySeed.CreatedAt),
            CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);
    }
}
