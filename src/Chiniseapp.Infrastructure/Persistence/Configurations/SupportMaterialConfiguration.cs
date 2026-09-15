using Chiniseapp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chiniseapp.Infrastructure.Persistence.Configurations;

public class SupportMaterialConfiguration : IEntityTypeConfiguration<SupportMaterial>
{
    public void Configure(EntityTypeBuilder<SupportMaterial> b)
    {
        b.ToTable("support_materials");
        b.HasKey(x => x.Id);
        b.Property(x => x.Language).HasMaxLength(20);
        b.Property(x => x.CommentText).IsRequired();
        b.HasOne<Entry>().WithMany().HasForeignKey(x => x.EntryId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Editor>().WithMany().HasForeignKey(x => x.AuthorEditorId).OnDelete(DeleteBehavior.Restrict);
    }
}
