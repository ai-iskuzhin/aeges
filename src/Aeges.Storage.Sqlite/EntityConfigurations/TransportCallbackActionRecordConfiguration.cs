using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aeges.Storage.Sqlite.EntityConfigurations;

internal sealed class TransportCallbackActionRecordConfiguration : IEntityTypeConfiguration<TransportCallbackActionRecord>
{
    public void Configure(EntityTypeBuilder<TransportCallbackActionRecord> builder)
    {
        builder.ToTable("transport_callback_actions");
        builder.HasKey(action => new { action.Transport, action.Token });

        builder.Property(action => action.Token).HasColumnName("token");
        builder.Property(action => action.Transport).HasColumnName("transport");
        builder.Property(action => action.Scope).HasColumnName("scope").IsRequired();
        builder.Property(action => action.ActionType).HasColumnName("action_type").IsRequired();
        builder.Property(action => action.PayloadJson).HasColumnName("payload_json").IsRequired();
        builder.Property(action => action.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(action => action.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(action => action.LastUsedAt).HasColumnName("last_used_at");
        builder.Property(action => action.UseCount).HasColumnName("use_count").IsRequired().HasDefaultValue(0);

        builder.HasIndex(action => action.ExpiresAt);
        builder.HasIndex(action => new { action.Transport, action.Scope });
    }
}
