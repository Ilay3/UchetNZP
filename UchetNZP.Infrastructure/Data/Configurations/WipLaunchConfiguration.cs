using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class WipLaunchConfiguration : IEntityTypeConfiguration<WipLaunch>
{
    public void Configure(EntityTypeBuilder<WipLaunch> builder)
    {
        builder.ToTable("WipLaunches");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.LaunchDate)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.FromOpNumber)
            .IsRequired();

        builder.Property(x => x.Quantity)
            .HasPrecision(12, 3);

        builder.Property(x => x.SumHoursToFinish)
            .HasPrecision(12, 3);

        builder.Property(x => x.Comment)
            .HasMaxLength(256);

        builder.HasIndex(x => new { x.PartId, x.SectionId, x.LaunchDate });

        builder.HasOne(x => x.Part)
            .WithMany(x => x.WipLaunches)
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Section)
            .WithMany(x => x.WipLaunches)
            .HasForeignKey(x => x.SectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
