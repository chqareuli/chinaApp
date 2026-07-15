using Chiniseapp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chiniseapp.Infrastructure.Persistence.Configurations;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_log_entries");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.EntityType)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(a => a.Action)
            .HasColumnType("audit_action")
            .IsRequired();

        builder.Property(a => a.OldValue).HasColumnType("jsonb");
        builder.Property(a => a.NewValue).HasColumnType("jsonb");

        builder.HasOne<Editor>().WithMany()
            .HasForeignKey(a => a.PerformedByEditorId)
            .OnDelete(DeleteBehavior.Restrict);

        // No FK to the audited entity itself (EntityType/EntityId are a generic
        // pointer) — audit rows must outlive whatever they describe, including
        // archived/repaired entries.
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}
