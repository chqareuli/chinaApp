using Chiniseapp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chiniseapp.Infrastructure.Persistence.Configurations;

public class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("media_assets");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.OriginalFilename).IsRequired();
        builder.Property(m => m.SafeStorageName).IsRequired();
        builder.Property(m => m.MimeType).IsRequired().HasMaxLength(255);
        builder.Property(m => m.StorageUrl).IsRequired();

        builder.Property(m => m.FileType)
            .HasColumnType("media_file_type")
            .IsRequired();

        builder.HasOne<Segment>().WithMany()
            .HasForeignKey(m => m.InsertedIntoSegmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Editor>().WithMany()
            .HasForeignKey(m => m.UploadedByEditorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
