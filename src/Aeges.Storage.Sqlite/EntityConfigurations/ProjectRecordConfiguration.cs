using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aeges.Storage.Sqlite.EntityConfigurations;

internal sealed class ProjectRecordConfiguration : IEntityTypeConfiguration<ProjectRecord>
{
    public void Configure(EntityTypeBuilder<ProjectRecord> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(project => project.Id);

        builder.Property(project => project.Id).HasColumnName("id");
        builder.Property(project => project.Name).HasColumnName("name").IsRequired();
        builder.Property(project => project.Path).HasColumnName("path").IsRequired();
        builder.Property(project => project.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(project => project.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(project => project.IsArchived).HasColumnName("is_archived").IsRequired().HasDefaultValue(false);
        builder.Property(project => project.ArchivedAt).HasColumnName("archived_at");

        builder.HasIndex(project => project.Path).IsUnique();
        builder.HasIndex(project => project.IsArchived);
    }
}
