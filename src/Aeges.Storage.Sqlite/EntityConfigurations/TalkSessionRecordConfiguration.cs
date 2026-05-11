using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aeges.Storage.Sqlite.EntityConfigurations;

internal sealed class TalkSessionRecordConfiguration : IEntityTypeConfiguration<TalkSessionRecord>
{
    public void Configure(EntityTypeBuilder<TalkSessionRecord> builder)
    {
        builder.ToTable("talk_sessions");
        builder.HasKey(session => session.Id);

        builder.Property(session => session.Id).HasColumnName("id");
        builder.Property(session => session.Source).HasColumnName("source").IsRequired();
        builder.Property(session => session.Title).HasColumnName("title").IsRequired();
        builder.Property(session => session.RunnerId).HasColumnName("runner_id").IsRequired();
        builder.Property(session => session.Status).HasColumnName("status").IsRequired();
        builder.Property(session => session.ExternalSessionId).HasColumnName("external_session_id");
        builder.Property(session => session.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(session => session.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(session => session.ArchivedAt).HasColumnName("archived_at");

        builder.HasIndex(session => session.Source);
        builder.HasIndex(session => session.Status);
        builder.HasIndex(session => session.UpdatedAt);
    }
}
