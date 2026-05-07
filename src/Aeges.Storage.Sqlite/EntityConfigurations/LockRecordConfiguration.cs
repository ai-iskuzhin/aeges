using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aeges.Storage.Sqlite.EntityConfigurations;

internal sealed class LockRecordConfiguration : IEntityTypeConfiguration<LockRecord>
{
    public void Configure(EntityTypeBuilder<LockRecord> builder)
    {
        builder.ToTable("locks");
        builder.HasKey(runtimeLock => runtimeLock.Id);

        builder.Property(runtimeLock => runtimeLock.Id).HasColumnName("id");
        builder.Property(runtimeLock => runtimeLock.TaskId).HasColumnName("task_id").IsRequired();
        builder.Property(runtimeLock => runtimeLock.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(runtimeLock => runtimeLock.PathPattern).HasColumnName("path_pattern").IsRequired();
        builder.Property(runtimeLock => runtimeLock.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(runtimeLock => runtimeLock.ReleasedAt).HasColumnName("released_at");

        builder
            .HasOne(runtimeLock => runtimeLock.Task)
            .WithMany(task => task.Locks)
            .HasForeignKey(runtimeLock => runtimeLock.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(runtimeLock => runtimeLock.Project)
            .WithMany(project => project.Locks)
            .HasForeignKey(runtimeLock => runtimeLock.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(runtimeLock => runtimeLock.TaskId);
        builder.HasIndex(runtimeLock => runtimeLock.ProjectId);
        builder.HasIndex(runtimeLock => runtimeLock.ReleasedAt);
    }
}
