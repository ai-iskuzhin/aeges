using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aeges.Storage.Sqlite.EntityConfigurations;

internal sealed class RuntimeEventRecordConfiguration : IEntityTypeConfiguration<RuntimeEventRecord>
{
    public void Configure(EntityTypeBuilder<RuntimeEventRecord> builder)
    {
        builder.ToTable("runtime_events");
        builder.HasKey(runtimeEvent => runtimeEvent.Id);

        builder.Property(runtimeEvent => runtimeEvent.Id).HasColumnName("id");
        builder.Property(runtimeEvent => runtimeEvent.TaskId).HasColumnName("task_id");
        builder.Property(runtimeEvent => runtimeEvent.IterationId).HasColumnName("iteration_id");
        builder.Property(runtimeEvent => runtimeEvent.MachineId).HasColumnName("machine_id");
        builder.Property(runtimeEvent => runtimeEvent.EventType).HasColumnName("event_type").IsRequired();
        builder.Property(runtimeEvent => runtimeEvent.Message).HasColumnName("message").IsRequired();
        builder.Property(runtimeEvent => runtimeEvent.PayloadJson).HasColumnName("payload_json");
        builder.Property(runtimeEvent => runtimeEvent.CreatedAt).HasColumnName("created_at").IsRequired();

        builder
            .HasOne(runtimeEvent => runtimeEvent.Task)
            .WithMany(task => task.RuntimeEvents)
            .HasForeignKey(runtimeEvent => runtimeEvent.TaskId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(runtimeEvent => runtimeEvent.Iteration)
            .WithMany(iteration => iteration.RuntimeEvents)
            .HasForeignKey(runtimeEvent => runtimeEvent.IterationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(runtimeEvent => runtimeEvent.Machine)
            .WithMany(machine => machine.RuntimeEvents)
            .HasForeignKey(runtimeEvent => runtimeEvent.MachineId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(runtimeEvent => runtimeEvent.TaskId);
        builder.HasIndex(runtimeEvent => runtimeEvent.IterationId);
        builder.HasIndex(runtimeEvent => runtimeEvent.MachineId);
        builder.HasIndex(runtimeEvent => runtimeEvent.EventType);
        builder.HasIndex(runtimeEvent => runtimeEvent.CreatedAt);
    }
}
