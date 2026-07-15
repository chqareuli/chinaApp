using Chiniseapp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chiniseapp.Infrastructure.Persistence.Configurations;

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("comments");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CommentText).IsRequired();

        builder.Property(c => c.Status)
            .HasColumnType("comment_status")
            .IsRequired();

        builder.Property(c => c.ParsedCommentParts).HasColumnType("jsonb");

        builder.HasOne<Entry>().WithMany()
            .HasForeignKey(c => c.EntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Segment>().WithMany()
            .HasForeignKey(c => c.TargetSegmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Editor>().WithMany()
            .HasForeignKey(c => c.AuthorEditorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.EntryId);
    }
}
