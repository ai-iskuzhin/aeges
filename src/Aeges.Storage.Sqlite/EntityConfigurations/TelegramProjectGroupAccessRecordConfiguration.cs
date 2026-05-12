using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aeges.Storage.Sqlite.EntityConfigurations;

internal sealed class TelegramProjectGroupAccessRecordConfiguration : IEntityTypeConfiguration<TelegramProjectGroupAccessRecord>
{
    public void Configure(EntityTypeBuilder<TelegramProjectGroupAccessRecord> builder)
    {
        builder.ToTable("telegram_project_group_access");
        builder.HasKey(access => new { access.UserId, access.ProjectGroupId });

        builder.Property(access => access.UserId).HasColumnName("user_id");
        builder.Property(access => access.ProjectGroupId).HasColumnName("project_group_id");
        builder.Property(access => access.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(access => access.ProjectGroupId);

        builder.HasOne(access => access.User)
            .WithMany(user => user.ProjectGroupAccess)
            .HasForeignKey(access => access.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(access => access.ProjectGroup)
            .WithMany()
            .HasForeignKey(access => access.ProjectGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
