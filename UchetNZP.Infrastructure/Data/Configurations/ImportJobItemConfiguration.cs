using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class ImportJobItemConfiguration : IEntityTypeConfiguration<ImportJobItem>
{
    public void Configure(EntityTypeBuilder<ImportJobItem> builder)
    {
        builder.ToTable("ImportJobItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ExternalId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.Payload)
            .HasMaxLength(4096);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(1024);

        builder.HasIndex(x => new { x.ImportJobId, x.ExternalId })
            .IsUnique();
    }
}
