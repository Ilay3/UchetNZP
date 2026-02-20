using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class WipBalanceCleanupJobConfiguration : IEntityTypeConfiguration<WipBalanceCleanupJob>
{
    public void Configure(EntityTypeBuilder<WipBalanceCleanupJob> builder)
    {
        builder.ToTable("WipBalanceCleanupJobs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.MinQuantity)
            .HasPrecision(12, 3);

        builder.Property(x => x.AffectedQuantity)
            .HasPrecision(12, 3);

        builder.Property(x => x.Comment)
            .HasMaxLength(512);

        builder.HasIndex(x => x.CreatedAt);

        builder.HasMany(x => x.StageItems)
            .WithOne(x => x.Job)
            .HasForeignKey(x => x.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
