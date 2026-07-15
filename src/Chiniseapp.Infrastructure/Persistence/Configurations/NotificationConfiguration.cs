using Chiniseapp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chiniseapp.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Type)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasOne<Editor>().WithMany()
            .HasForeignKey(n => n.RecipientEditorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Editor>().WithMany()
            .HasForeignKey(n => n.TriggeredByEditorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Entry>().WithMany()
            .HasForeignKey(n => n.EntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => new { n.RecipientEditorId, n.IsRead });
    }
}
