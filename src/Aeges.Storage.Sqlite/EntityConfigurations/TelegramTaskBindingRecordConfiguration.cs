using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aeges.Storage.Sqlite.EntityConfigurations;

internal sealed class TelegramTaskBindingRecordConfiguration : IEntityTypeConfiguration<TelegramTaskBindingRecord>
{
    public void Configure(EntityTypeBuilder<TelegramTaskBindingRecord> builder)
    {
        builder.ToTable("telegram_task_bindings");
        builder.HasKey(binding => new { binding.ChatId, binding.MessageThreadId, binding.TaskId });

        builder.Property(binding => binding.ChatId).HasColumnName("chat_id").IsRequired();
        builder.Property(binding => binding.MessageThreadId).HasColumnName("message_thread_id");
        builder.Property(binding => binding.TaskId).HasColumnName("task_id").IsRequired();
        builder.Property(binding => binding.DetailMessageId).HasColumnName("detail_message_id");
        builder.Property(binding => binding.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(binding => binding.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(binding => binding.TaskId);

        builder.HasOne(binding => binding.Task)
            .WithMany()
            .HasForeignKey(binding => binding.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
