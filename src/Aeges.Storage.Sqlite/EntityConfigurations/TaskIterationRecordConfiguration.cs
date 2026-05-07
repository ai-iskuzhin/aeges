using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aeges.Storage.Sqlite.EntityConfigurations;

internal sealed class TaskIterationRecordConfiguration : IEntityTypeConfiguration<TaskIterationRecord>
{
    public void Configure(EntityTypeBuilder<TaskIterationRecord> builder)
    {
        builder.ToTable("task_iterations");
        builder.HasKey(iteration => iteration.Id);

        builder.Property(iteration => iteration.Id).HasColumnName("id");
        builder.Property(iteration => iteration.TaskId).HasColumnName("task_id").IsRequired();
        builder.Property(iteration => iteration.IterationNumber).HasColumnName("iteration_number").IsRequired();
        builder.Property(iteration => iteration.Status)
            .HasColumnName("status")
            .HasConversion(new TaskIterationStatusStorageConverter())
            .IsRequired();
        builder.Property(iteration => iteration.RunnerId).HasColumnName("runner_id").IsRequired();
        builder.Property(iteration => iteration.WorktreePath).HasColumnName("worktree_path");
        builder.Property(iteration => iteration.PromptArtifactId).HasColumnName("prompt_artifact_id");
        builder.Property(iteration => iteration.ResultArtifactId).HasColumnName("result_artifact_id");
        builder.Property(iteration => iteration.DiffArtifactId).HasColumnName("diff_artifact_id");
        builder.Property(iteration => iteration.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(iteration => iteration.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(iteration => iteration.StartedAt).HasColumnName("started_at");
        builder.Property(iteration => iteration.CompletedAt).HasColumnName("completed_at");
        builder.Property(iteration => iteration.FailureReason).HasColumnName("failure_reason");

        builder
            .HasOne(iteration => iteration.Task)
            .WithMany(task => task.Iterations)
            .HasForeignKey(iteration => iteration.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(iteration => iteration.TaskId);
        builder.HasIndex(iteration => new { iteration.TaskId, iteration.IterationNumber }).IsUnique();
        builder.HasIndex(iteration => iteration.Status);
    }
}
