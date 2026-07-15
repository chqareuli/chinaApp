using Chiniseapp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chiniseapp.Infrastructure.Persistence.Configurations;

public class ControlledVocabularyConfiguration : IEntityTypeConfiguration<ControlledVocabulary>
{
    public void Configure(EntityTypeBuilder<ControlledVocabulary> builder)
    {
        builder.ToTable("controlled_vocabularies");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Category)
            .HasColumnType("vocabulary_category")
            .IsRequired();

        builder.Property(v => v.Code)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(v => v.DisplayZh)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(v => v.DisplayKa)
            .HasMaxLength(255);

        builder.HasIndex(v => new { v.Category, v.Code }).IsUnique();
    }
}
