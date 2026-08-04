using Chiniseapp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chiniseapp.Infrastructure.Persistence.Configurations;

public class EntrySupportMaterialConfiguration : IEntityTypeConfiguration<EntrySupportMaterial>
{
    public void Configure(EntityTypeBuilder<EntrySupportMaterial> builder)
    {
        builder.ToTable("entry_support_materials");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Language)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(item => item.CommentText)
            .IsRequired()
            .HasMaxLength(4000);

        builder.HasOne(item => item.Entry)
            .WithMany(entry => entry.SupportMaterials)
            .HasForeignKey(item => item.EntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.AddedByEditor)
            .WithMany()
            .HasForeignKey(item => item.AddedByEditorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.EntryId, item.CreatedAtUtc });
        builder.HasIndex(item => item.AddedByEditorId);
    }
}
