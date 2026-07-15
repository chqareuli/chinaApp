using Chiniseapp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chiniseapp.Infrastructure.Persistence.Configurations;

public class ReferenceMaterialConfiguration : IEntityTypeConfiguration<ReferenceMaterial>
{
    public void Configure(EntityTypeBuilder<ReferenceMaterial> builder)
    {
        builder.ToTable("reference_materials");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.G1).IsRequired();
        builder.Property(r => r.G2).IsRequired();
        builder.Property(r => r.G3).IsRequired();
        builder.Property(r => r.G4).IsRequired();
        builder.Property(r => r.G5).IsRequired();
        builder.Property(r => r.OriginalRawReferenceMaterial).IsRequired();

        builder.HasOne<Entry>().WithOne()
            .HasForeignKey<ReferenceMaterial>(r => r.EntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.EntryId).IsUnique();
    }
}
