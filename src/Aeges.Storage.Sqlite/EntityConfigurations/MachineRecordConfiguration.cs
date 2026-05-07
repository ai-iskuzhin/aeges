using Aeges.Storage.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aeges.Storage.Sqlite.EntityConfigurations;

internal sealed class MachineRecordConfiguration : IEntityTypeConfiguration<MachineRecord>
{
    public void Configure(EntityTypeBuilder<MachineRecord> builder)
    {
        builder.ToTable("machines");
        builder.HasKey(machine => machine.Id);

        builder.Property(machine => machine.Id).HasColumnName("id");
        builder.Property(machine => machine.Name).HasColumnName("name").IsRequired();
        builder.Property(machine => machine.Platform).HasColumnName("platform").IsRequired();
        builder.Property(machine => machine.Status).HasColumnName("status").IsRequired();
        builder.Property(machine => machine.LastSeenAt).HasColumnName("last_seen_at");
        builder.Property(machine => machine.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(machine => machine.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(machine => machine.Status);
    }
}
