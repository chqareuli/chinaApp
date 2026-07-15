using Chiniseapp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chiniseapp.Infrastructure.Persistence.Configurations;

public class DirectMessageConfiguration : IEntityTypeConfiguration<DirectMessage>
{
    public void Configure(EntityTypeBuilder<DirectMessage> builder)
    {
        builder.ToTable("direct_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Body).IsRequired();

        builder.HasOne<Editor>().WithMany()
            .HasForeignKey(m => m.SenderEditorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Editor>().WithMany()
            .HasForeignKey(m => m.RecipientEditorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.RecipientEditorId, m.ReadAtUtc });
    }
}
