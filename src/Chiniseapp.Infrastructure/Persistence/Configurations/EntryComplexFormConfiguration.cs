using Chiniseapp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chiniseapp.Infrastructure.Persistence.Configurations;

public class EntryComplexFormConfiguration : IEntityTypeConfiguration<EntryComplexForm>
{
    public void Configure(EntityTypeBuilder<EntryComplexForm> builder)
    {
        builder.ToTable("entry_complex_forms");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Value)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(item => item.NormalizedValue)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasOne(item => item.Entry)
            .WithMany(entry => entry.ComplexForms)
            .HasForeignKey(item => item.EntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new { item.EntryId, item.OrderIndex });
        builder.HasIndex(item => new { item.EntryId, item.NormalizedValue }).IsUnique();
    }
}
