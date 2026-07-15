using Chiniseapp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chiniseapp.Infrastructure.Persistence.Configurations;

public class EntryConfiguration : IEntityTypeConfiguration<Entry>
{
    public void Configure(EntityTypeBuilder<Entry> builder)
    {
        builder.ToTable("entries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Lemma)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.Pinyin)
            .HasMaxLength(255);

        builder.Property(e => e.Status)
            .HasColumnType("entry_status")
            .IsRequired();

        builder.Property(e => e.SearchNormalizedTitle)
            .IsRequired();

        // Postgres system column, exposed as a shadow property; Npgsql
        // recognizes a uint "xmin" property marked IsRowVersion() as the
        // system column automatically.
        builder.Property<uint>("xmin").IsRowVersion();

        builder.HasOne<Editor>().WithMany()
            .HasForeignKey(e => e.CreatedByEditorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Editor>().WithMany()
            .HasForeignKey(e => e.MainAuthorEditorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Editor>().WithMany()
            .HasForeignKey(e => e.LastEditorEditorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Entry <-> Segments relationship is configured from the Segment side
        // (SegmentConfiguration), since Segment.EntryId is the owning FK.

        // Starts-with prefix search (5.2 search dropdown algorithm) — btree with
        // text_pattern_ops is the right operator class for LIKE 'prefix%'.
        builder.HasIndex(e => e.SearchNormalizedTitle)
            .HasMethod("btree")
            .HasOperators("text_pattern_ops");

        // Editor main-list sort (status priority + last_modified desc), excluding
        // new_entry from that list.
        builder.HasIndex(e => new { e.StatusPriority, e.UpdatedAtUtc })
            .HasFilter("status <> 'new_entry'");
    }
}
