using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> builder)
    {
        builder.ToTable("ImportJobs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(1024);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.ImportJob)
            .HasForeignKey(x => x.ImportJobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
