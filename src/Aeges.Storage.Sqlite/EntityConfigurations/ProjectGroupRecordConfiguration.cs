using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aeges.Storage.Sqlite.EntityConfigurations;

internal sealed class ProjectGroupRecordConfiguration : IEntityTypeConfiguration<ProjectGroupRecord>
{
    public void Configure(EntityTypeBuilder<ProjectGroupRecord> builder)
    {
        builder.ToTable("project_groups");
        builder.HasKey(group => group.Id);

        builder.Property(group => group.Id).HasColumnName("id");
        builder.Property(group => group.Name).HasColumnName("name").IsRequired();
        builder.Property(group => group.Path).HasColumnName("path");
        builder.Property(group => group.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(group => group.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(group => group.IsArchived).HasColumnName("is_archived").IsRequired().HasDefaultValue(false);
        builder.Property(group => group.ArchivedAt).HasColumnName("archived_at");

        builder.HasIndex(group => group.Name).IsUnique();
        builder.HasIndex(group => group.Path).IsUnique();
        builder.HasIndex(group => group.IsArchived);
    }
}
