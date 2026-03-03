using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class WipLabelLedgerConfiguration : IEntityTypeConfiguration<WipLabelLedger>
{
    public void Configure(EntityTypeBuilder<WipLabelLedger> builder)
    {
        builder.ToTable("WipLabelLedger");

        builder.HasKey(x => x.EventId);

        builder.Property(x => x.EventType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Qty)
            .HasPrecision(12, 3);

        builder.Property(x => x.ScrapQty)
            .HasPrecision(12, 3);

        builder.Property(x => x.RefEntityType)
            .HasMaxLength(40)
            .IsRequired();

        builder.HasIndex(x => x.TransactionId);
        builder.HasIndex(x => x.FromLabelId);
        builder.HasIndex(x => x.ToLabelId);
        builder.HasIndex(x => x.EventTime);
    }
}
