using Chiniseapp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chiniseapp.Infrastructure.Persistence.Configurations;

public class ScoreEntryConfiguration : IEntityTypeConfiguration<ScoreEntry>
{
    public void Configure(EntityTypeBuilder<ScoreEntry> builder)
    {
        builder.ToTable("score_entries");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.ScoreType)
            .HasColumnType("score_type")
            .IsRequired();

        builder.HasOne<Entry>().WithMany()
            .HasForeignKey(s => s.EntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Editor>().WithMany()
            .HasForeignKey(s => s.EditorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Enforces "once per entry per editor per score type" at the DB level.
        builder.HasIndex(s => new { s.EntryId, s.EditorId, s.ScoreType }).IsUnique();
    }
}
