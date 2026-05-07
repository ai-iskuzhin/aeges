using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aeges.Storage.Sqlite.EntityConfigurations;

internal sealed class RunnerExecutionRecordConfiguration : IEntityTypeConfiguration<RunnerExecutionRecord>
{
    public void Configure(EntityTypeBuilder<RunnerExecutionRecord> builder)
    {
        builder.ToTable("runner_executions");
        builder.HasKey(execution => execution.Id);

        builder.Property(execution => execution.Id).HasColumnName("id");
        builder.Property(execution => execution.TaskId).HasColumnName("task_id").IsRequired();
        builder.Property(execution => execution.IterationId).HasColumnName("iteration_id").IsRequired();
        builder.Property(execution => execution.RunnerId).HasColumnName("runner_id").IsRequired();
        builder.Property(execution => execution.Command).HasColumnName("command").IsRequired();
        builder.Property(execution => execution.WorkingDirectory).HasColumnName("working_directory").IsRequired();
        builder.Property(execution => execution.ExitCode).HasColumnName("exit_code");
        builder.Property(execution => execution.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(execution => execution.CompletedAt).HasColumnName("completed_at");
        builder.Property(execution => execution.TimedOut).HasColumnName("timed_out").HasDefaultValue(false);
        builder.Property(execution => execution.Cancelled).HasColumnName("cancelled").HasDefaultValue(false);

        builder
            .HasOne(execution => execution.Task)
            .WithMany(task => task.RunnerExecutions)
            .HasForeignKey(execution => execution.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(execution => execution.Iteration)
            .WithMany(iteration => iteration.RunnerExecutions)
            .HasForeignKey(execution => execution.IterationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(execution => execution.TaskId);
        builder.HasIndex(execution => execution.IterationId);
        builder.HasIndex(execution => execution.RunnerId);
    }
}
