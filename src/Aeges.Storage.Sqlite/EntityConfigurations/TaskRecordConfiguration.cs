using Aeges.Core;
using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aeges.Storage.Sqlite.EntityConfigurations;

internal sealed class TaskRecordConfiguration : IEntityTypeConfiguration<TaskRecord>
{
    public void Configure(EntityTypeBuilder<TaskRecord> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(task => task.Id);

        builder.Property(task => task.Id).HasColumnName("id");
        builder.Property(task => task.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(task => task.MachineId).HasColumnName("machine_id").IsRequired();
        builder.Property(task => task.Title).HasColumnName("title").IsRequired();
        builder.Property(task => task.Goal).HasColumnName("goal").IsRequired();
        builder.Property(task => task.Status)
            .HasColumnName("status")
            .HasConversion(new RuntimeTaskStatusStorageConverter())
            .IsRequired();
        builder.Property(task => task.Priority).HasColumnName("priority").HasDefaultValue(0);
        builder.Property(task => task.MaxIterations).HasColumnName("max_iterations").HasDefaultValue(RuntimeTask.DefaultMaxIterations);
        builder.Property(task => task.CurrentIteration).HasColumnName("current_iteration").HasDefaultValue(0);
        builder.Property(task => task.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(task => task.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(task => task.StartedAt).HasColumnName("started_at");
        builder.Property(task => task.CompletedAt).HasColumnName("completed_at");
        builder.Property(task => task.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(task => task.FailureReason).HasColumnName("failure_reason");

        builder
            .HasOne(task => task.Project)
            .WithMany(project => project.Tasks)
            .HasForeignKey(task => task.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(task => task.Machine)
            .WithMany(machine => machine.Tasks)
            .HasForeignKey(task => task.MachineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(task => task.ProjectId);
        builder.HasIndex(task => task.MachineId);
        builder.HasIndex(task => task.Status);
        builder.HasIndex(task => new { task.Status, task.Priority, task.CreatedAt });
    }
}
