using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class WipBalanceConfiguration : IEntityTypeConfiguration<WipBalance>
{
    public void Configure(EntityTypeBuilder<WipBalance> builder)
    {
        builder.ToTable("WipBalances");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OpNumber)
            .IsRequired();

        builder.Property(x => x.Quantity)
            .HasPrecision(12, 3);

        builder.HasIndex(x => new { x.PartId, x.SectionId, x.OpNumber })
            .IsUnique();

        builder.HasOne(x => x.Part)
            .WithMany(x => x.WipBalances)
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Section)
            .WithMany(x => x.WipBalances)
            .HasForeignKey(x => x.SectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
