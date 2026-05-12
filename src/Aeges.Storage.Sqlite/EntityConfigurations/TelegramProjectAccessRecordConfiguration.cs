using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aeges.Storage.Sqlite.EntityConfigurations;

internal sealed class TelegramProjectAccessRecordConfiguration : IEntityTypeConfiguration<TelegramProjectAccessRecord>
{
    public void Configure(EntityTypeBuilder<TelegramProjectAccessRecord> builder)
    {
        builder.ToTable("telegram_project_access");
        builder.HasKey(access => new { access.UserId, access.ProjectId });

        builder.Property(access => access.UserId).HasColumnName("user_id");
        builder.Property(access => access.ProjectId).HasColumnName("project_id");
        builder.Property(access => access.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(access => access.ProjectId);

        builder.HasOne(access => access.User)
            .WithMany(user => user.ProjectAccess)
            .HasForeignKey(access => access.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(access => access.Project)
            .WithMany()
            .HasForeignKey(access => access.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
