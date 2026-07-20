using Chiniseapp.Application.ReferenceMaterials;
using Chiniseapp.Domain.Rules;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
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

    public async Task<ReferenceMaterialDetail> SaveAsync(
        int entryId,
        SaveReferenceMaterialRequest request,
        int currentEditorId,
        CancellationToken ct = default)
    {
        var entry = await db.Entries.FindAsync([entryId], ct);
        if (entry is null)
        {
            throw new ReferenceMaterialValidationException($"Entry {entryId} does not exist.");
        }

        var actor = await db.Editors.FindAsync([currentEditorId], ct)
            ?? throw new ReferenceMaterialValidationException("Current editor not found.");
        if (!EntryContentRules.CanEdit(actor.Role, entry.Status))
        {
            throw new ReferenceMaterialAccessDeniedException(
                $"Your role cannot edit reference material while the entry is in '{entry.Status}' status.");
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

        entry.LastEditorEditorId = currentEditorId;
        entry.UpdatedAtUtc = now;
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            EntityType = nameof(Entry),
            EntityId = entry.Id,
            Action = AuditAction.ContentEdited,
            PerformedByEditorId = currentEditorId,
            PerformedAtUtc = now,
            Notes = "Reference material updated.",
        });

        await db.SaveChangesAsync(ct);
        return ToDetail(material);
    }

    private static ReferenceMaterialDetail ToDetail(Domain.Entities.ReferenceMaterial m) => new(
        m.EntryId, m.G1, m.G2, m.G3, m.G4, m.G5, m.OriginalRawReferenceMaterial, m.UpdatedAtUtc);
}
