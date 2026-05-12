using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aeges.Storage.Sqlite.EntityConfigurations;

internal sealed class TelegramUserRecordConfiguration : IEntityTypeConfiguration<TelegramUserRecord>
{
    public void Configure(EntityTypeBuilder<TelegramUserRecord> builder)
    {
        builder.ToTable("telegram_users");
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id).HasColumnName("id");
        builder.Property(user => user.ChatId).HasColumnName("chat_id").IsRequired();
        builder.Property(user => user.Role)
            .HasColumnName("role")
            .HasConversion<TelegramUserRoleStorageConverter>()
            .IsRequired();
        builder.Property(user => user.Status)
            .HasColumnName("status")
            .HasConversion<TelegramUserStatusStorageConverter>()
            .IsRequired();
        builder.Property(user => user.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(user => user.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(user => user.ChatId).IsUnique();
        builder.HasIndex(user => user.Role);
        builder.HasIndex(user => user.Status);
    }
}
