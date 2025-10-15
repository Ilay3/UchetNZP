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

        builder.Property(x => x.RowIndex)
            .IsRequired()
            .HasColumnType("integer");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.Message)
            .HasMaxLength(1024);

        builder.HasIndex(x => new { x.ImportJobId, x.RowIndex })
            .IsUnique();
    }
}
