using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aeges.Storage.Sqlite.EntityConfigurations;

internal sealed class TalkMessageRecordConfiguration : IEntityTypeConfiguration<TalkMessageRecord>
{
    public void Configure(EntityTypeBuilder<TalkMessageRecord> builder)
    {
        builder.ToTable("talk_messages");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id).HasColumnName("id");
        builder.Property(message => message.SessionId).HasColumnName("session_id").IsRequired();
        builder.Property(message => message.Role).HasColumnName("role").IsRequired();
        builder.Property(message => message.Content).HasColumnName("content").IsRequired();
        builder.Property(message => message.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(message => message.SessionId);
        builder.HasIndex(message => message.CreatedAt);

        builder.HasOne(message => message.Session)
            .WithMany(session => session.Messages)
            .HasForeignKey(message => message.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
