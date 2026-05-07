using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aeges.Storage.Sqlite.EntityConfigurations;

internal sealed class ApprovalRecordConfiguration : IEntityTypeConfiguration<ApprovalRecord>
{
    public void Configure(EntityTypeBuilder<ApprovalRecord> builder)
    {
        builder.ToTable("approvals");
        builder.HasKey(approval => approval.Id);

        builder.Property(approval => approval.Id).HasColumnName("id");
        builder.Property(approval => approval.TaskId).HasColumnName("task_id").IsRequired();
        builder.Property(approval => approval.IterationId).HasColumnName("iteration_id");
        builder.Property(approval => approval.Status).HasColumnName("status").IsRequired();
        builder.Property(approval => approval.Reason).HasColumnName("reason").IsRequired();
        builder.Property(approval => approval.RequestedAction).HasColumnName("requested_action").IsRequired();
        builder.Property(approval => approval.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(approval => approval.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(approval => approval.ResolvedBy).HasColumnName("resolved_by");

        builder
            .HasOne(approval => approval.Task)
            .WithMany(task => task.Approvals)
            .HasForeignKey(approval => approval.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(approval => approval.Iteration)
            .WithMany(iteration => iteration.Approvals)
            .HasForeignKey(approval => approval.IterationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(approval => approval.TaskId);
        builder.HasIndex(approval => approval.IterationId);
        builder.HasIndex(approval => approval.Status);
    }
}
