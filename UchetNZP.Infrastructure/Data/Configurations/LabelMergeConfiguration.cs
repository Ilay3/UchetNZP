using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class LabelMergeConfiguration : IEntityTypeConfiguration<LabelMerge>
{
    public void Configure(EntityTypeBuilder<LabelMerge> builder)
    {
        builder.ToTable("LabelMerges");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne(x => x.InputLabel)
            .WithMany(x => x.MergeOutputs)
            .HasForeignKey(x => x.InputLabelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.OutputLabel)
            .WithMany(x => x.MergeInputs)
            .HasForeignKey(x => x.OutputLabelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.InputLabelId);
        builder.HasIndex(x => x.OutputLabelId);
        builder.HasIndex(x => new { x.InputLabelId, x.OutputLabelId }).IsUnique();
    }
}
