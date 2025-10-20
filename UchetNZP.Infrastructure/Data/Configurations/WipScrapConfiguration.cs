using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class WipScrapConfiguration : IEntityTypeConfiguration<WipScrap>
{
    public void Configure(EntityTypeBuilder<WipScrap> builder)
    {
        builder.ToTable("WipScraps");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity)
            .HasPrecision(12, 3);

        builder.Property(x => x.RecordedAt)
            .IsRequired();

        builder.Property(x => x.ScrapType)
            .HasConversion<int>();

        builder.HasIndex(x => new { x.PartId, x.OpNumber });

        builder.HasOne(x => x.Part)
            .WithMany(x => x.WipScraps)
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Section)
            .WithMany(x => x.WipScraps)
            .HasForeignKey(x => x.SectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Transfer)
            .WithOne(x => x.Scrap)
            .HasForeignKey<WipScrap>(x => x.TransferId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
