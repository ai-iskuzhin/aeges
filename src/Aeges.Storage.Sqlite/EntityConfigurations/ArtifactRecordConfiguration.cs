using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aeges.Storage.Sqlite.EntityConfigurations;

internal sealed class ArtifactRecordConfiguration : IEntityTypeConfiguration<ArtifactRecord>
{
    public void Configure(EntityTypeBuilder<ArtifactRecord> builder)
    {
        builder.ToTable("artifacts");
        builder.HasKey(artifact => artifact.Id);

        builder.Property(artifact => artifact.Id).HasColumnName("id");
        builder.Property(artifact => artifact.TaskId).HasColumnName("task_id").IsRequired();
        builder.Property(artifact => artifact.IterationId).HasColumnName("iteration_id");
        builder.Property(artifact => artifact.Type).HasColumnName("type").IsRequired();
        builder.Property(artifact => artifact.RelativePath).HasColumnName("relative_path").IsRequired();
        builder.Property(artifact => artifact.SizeBytes).HasColumnName("size_bytes");
        builder.Property(artifact => artifact.Sha256).HasColumnName("sha256");
        builder.Property(artifact => artifact.CreatedAt).HasColumnName("created_at").IsRequired();

        builder
            .HasOne(artifact => artifact.Task)
            .WithMany(task => task.Artifacts)
            .HasForeignKey(artifact => artifact.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(artifact => artifact.Iteration)
            .WithMany(iteration => iteration.Artifacts)
            .HasForeignKey(artifact => artifact.IterationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(artifact => artifact.TaskId);
        builder.HasIndex(artifact => artifact.IterationId);
        builder.HasIndex(artifact => artifact.Type);
    }
}
