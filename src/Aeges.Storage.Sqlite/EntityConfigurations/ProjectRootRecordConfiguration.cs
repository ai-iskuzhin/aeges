using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aeges.Storage.Sqlite.EntityConfigurations;

internal sealed class ProjectRootRecordConfiguration : IEntityTypeConfiguration<ProjectRootRecord>
{
    public void Configure(EntityTypeBuilder<ProjectRootRecord> builder)
    {
        builder.ToTable("project_roots");
        builder.HasKey(root => root.Id);

        builder.Property(root => root.Id).HasColumnName("id");
        builder.Property(root => root.Name).HasColumnName("name").IsRequired();
        builder.Property(root => root.Path).HasColumnName("path").IsRequired();
        builder.Property(root => root.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(root => root.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(root => root.IsArchived).HasColumnName("is_archived").IsRequired().HasDefaultValue(false);
        builder.Property(root => root.ArchivedAt).HasColumnName("archived_at");

        builder.HasIndex(root => root.Name).IsUnique();
        builder.HasIndex(root => root.Path).IsUnique();
        builder.HasIndex(root => root.IsArchived);
    }
}
