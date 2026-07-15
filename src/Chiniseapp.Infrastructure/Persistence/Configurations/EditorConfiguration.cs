using Chiniseapp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chiniseapp.Infrastructure.Persistence.Configurations;

public class EditorConfiguration : IEntityTypeConfiguration<Editor>
{
    public void Configure(EntityTypeBuilder<Editor> builder)
    {
        builder.ToTable("editors");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.PasswordHash).IsRequired();

        builder.Property(e => e.DisplayName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.Role)
            .HasColumnType("editor_role")
            .IsRequired();

        builder.HasIndex(e => e.Email).IsUnique();
    }
}
