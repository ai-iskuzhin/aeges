using Aeges.Core;

namespace Aeges.Core.Tests;

public sealed class ApprovalRequestTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 05, 07, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Create_initializes_pending_request()
    {
        var approval = CreateApproval();

        Assert.Equal(ApprovalStatus.Pending, approval.Status);
        Assert.Equal("Dependency change requires approval.", approval.Reason);
        Assert.Equal("Add EF Core SQLite package.", approval.RequestedAction);
        Assert.Equal(CreatedAt, approval.CreatedAt);
        Assert.Null(approval.ResolvedAt);
        Assert.Null(approval.ResolvedBy);
    }

    [Fact]
    public void Rehydrate_restores_approval_state()
    {
        var resolvedAt = CreatedAt.AddMinutes(1);

        var approval = ApprovalRequest.Rehydrate(
            new ApprovalId("approval-001"),
            new TaskId("task-001"),
            new IterationId("iteration-001"),
            ApprovalStatus.Approved,
            "Dependency change requires approval.",
            "Add EF Core SQLite package.",
            CreatedAt,
            resolvedAt,
            "operator");

        Assert.Equal(ApprovalStatus.Approved, approval.Status);
        Assert.Equal("Dependency change requires approval.", approval.Reason);
        Assert.Equal("Add EF Core SQLite package.", approval.RequestedAction);
        Assert.Equal(CreatedAt, approval.CreatedAt);
        Assert.Equal(resolvedAt, approval.ResolvedAt);
        Assert.Equal("operator", approval.ResolvedBy);
    }

    [Fact]
    public void Approve_resolves_request_once()
    {
        var approval = CreateApproval();
        var resolvedAt = CreatedAt.AddMinutes(1);

        approval.Approve("operator", resolvedAt);

        Assert.Equal(ApprovalStatus.Approved, approval.Status);
        Assert.Equal("operator", approval.ResolvedBy);
        Assert.Equal(resolvedAt, approval.ResolvedAt);
        Assert.True(approval.Status.IsTerminal());
        Assert.Throws<AegesDomainException>(() => approval.Reject("operator", resolvedAt.AddMinutes(1)));
    }

    [Fact]
    public void Reject_resolves_request()
    {
        var approval = CreateApproval();
        var resolvedAt = CreatedAt.AddMinutes(1);

        approval.Reject("operator", resolvedAt);

        Assert.Equal(ApprovalStatus.Rejected, approval.Status);
        Assert.Equal("operator", approval.ResolvedBy);
        Assert.Equal(resolvedAt, approval.ResolvedAt);
    }

    [Fact]
    public void Cancel_resolves_request()
    {
        var approval = CreateApproval();
        var resolvedAt = CreatedAt.AddMinutes(1);

        approval.Cancel("runtime", resolvedAt);

        Assert.Equal(ApprovalStatus.Cancelled, approval.Status);
        Assert.Equal("runtime", approval.ResolvedBy);
        Assert.Equal(resolvedAt, approval.ResolvedAt);
    }

    [Theory]
    [InlineData(ApprovalStatus.Pending, "pending")]
    [InlineData(ApprovalStatus.Approved, "approved")]
    [InlineData(ApprovalStatus.Rejected, "rejected")]
    [InlineData(ApprovalStatus.Cancelled, "cancelled")]
    public void Status_round_trips_storage_values(ApprovalStatus status, string storageValue)
    {
        Assert.Equal(storageValue, status.ToStorageValue());
        Assert.Equal(status, ApprovalStatusExtensions.FromStorageValue(storageValue));
    }

    private static ApprovalRequest CreateApproval() =>
        ApprovalRequest.Create(
            new ApprovalId("approval-001"),
            new TaskId("task-001"),
            new IterationId("iteration-001"),
            "Dependency change requires approval.",
            "Add EF Core SQLite package.",
            CreatedAt);
}
