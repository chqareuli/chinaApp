using Chiniseapp.Application.ReferenceMaterials;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Chiniseapp.Infrastructure.ReferenceMaterials;

public class ReferenceMaterialService(ChiniseDbContext db) : IReferenceMaterialService
{
    public async Task<ReferenceMaterialDetail?> GetForEntryAsync(int entryId, CancellationToken ct = default)
    {
        var material = await db.ReferenceMaterials.FirstOrDefaultAsync(r => r.EntryId == entryId, ct);
        return material is null ? null : ToDetail(material);
    }

    public async Task<ReferenceMaterialDetail> SaveAsync(int entryId, SaveReferenceMaterialRequest request, CancellationToken ct = default)
    {
        var entryExists = await db.Entries.AnyAsync(e => e.Id == entryId, ct);
        if (!entryExists)
        {
            throw new ReferenceMaterialValidationException($"Entry {entryId} does not exist.");
        }

        var material = await db.ReferenceMaterials.FirstOrDefaultAsync(r => r.EntryId == entryId, ct);
        var now = DateTime.UtcNow;
        if (material is null)
        {
            material = new Domain.Entities.ReferenceMaterial
            {
                EntryId = entryId,
                CreatedAtUtc = now,
            };
            db.ReferenceMaterials.Add(material);
        }

        material.G1 = request.G1 ?? string.Empty;
        material.G2 = request.G2 ?? string.Empty;
        material.G3 = request.G3 ?? string.Empty;
        material.G4 = request.G4 ?? string.Empty;
        material.G5 = request.G5 ?? string.Empty;
        material.UpdatedAtUtc = now;

        await db.SaveChangesAsync(ct);
        return ToDetail(material);
    }

    private static ReferenceMaterialDetail ToDetail(Domain.Entities.ReferenceMaterial m) => new(
        m.EntryId, m.G1, m.G2, m.G3, m.G4, m.G5, m.OriginalRawReferenceMaterial, m.UpdatedAtUtc);
}
