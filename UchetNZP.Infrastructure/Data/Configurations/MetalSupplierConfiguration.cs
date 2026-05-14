using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class MetalSupplierConfiguration : IEntityTypeConfiguration<MetalSupplier>
{
    public void Configure(EntityTypeBuilder<MetalSupplier> builder)
    {
        builder.ToTable("MetalSuppliers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Identifier)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.FullName)
            .HasMaxLength(512);

        builder.Property(x => x.Inn)
            .IsRequired()
            .HasMaxLength(12);

        builder.Property(x => x.Kpp)
            .HasMaxLength(9);

        builder.Property(x => x.LegalEntityKind)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.CountryOfRegistration)
            .HasMaxLength(128);

        builder.Property(x => x.Okpo)
            .HasMaxLength(16);

        builder.Property(x => x.MainBankAccount)
            .HasMaxLength(128);

        builder.Property(x => x.MainContractName)
            .HasMaxLength(256);

        builder.Property(x => x.ContactPerson)
            .HasMaxLength(256);

        builder.Property(x => x.AdditionalInfo)
            .HasMaxLength(1024);

        builder.Property(x => x.Comment)
            .HasMaxLength(512);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => x.Identifier)
            .IsUnique();

        builder.HasIndex(x => x.Inn);
    }
}
