using Chiniseapp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chiniseapp.Infrastructure.Persistence.Configurations;

public class EntryPronunciationConfiguration : IEntityTypeConfiguration<EntryPronunciation>
{
    public void Configure(EntityTypeBuilder<EntryPronunciation> builder)
    {
        builder.ToTable("entry_pronunciations");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Value)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(item => item.NormalizedValue)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasOne(item => item.Entry)
            .WithMany(entry => entry.Pronunciations)
            .HasForeignKey(item => item.EntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new { item.EntryId, item.OrderIndex });
        builder.HasIndex(item => new { item.EntryId, item.NormalizedValue }).IsUnique();
    }
}
