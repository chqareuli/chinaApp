using Chiniseapp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chiniseapp.Infrastructure.Persistence.Configurations;

public class SegmentConfiguration : IEntityTypeConfiguration<Segment>
{
    public void Configure(EntityTypeBuilder<Segment> builder)
    {
        builder.ToTable("segments");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.SegmentType)
            .HasColumnType("segment_type")
            .IsRequired();

        builder.Property(s => s.Placement)
            .HasColumnType("placement");

        builder.Property(s => s.Content)
            .HasColumnType("jsonb");

        builder.Property(s => s.Attributes)
            .HasColumnType("jsonb");

        builder.HasOne<Entry>()
            .WithMany(e => e.Segments)
            .HasForeignKey(s => s.EntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Parent)
            .WithMany(s => s.Children)
            .HasForeignKey(s => s.ParentSegmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.EntryId);
        builder.HasIndex(s => s.ParentSegmentId);

        // Ordered tree loads: all children of a given parent, in order.
        builder.HasIndex(s => new { s.EntryId, s.ParentSegmentId, s.OrderIndex });
    }
}
