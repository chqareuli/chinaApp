using Chiniseapp.Application.SupportMaterials;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Domain.Rules;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Chiniseapp.Infrastructure.SupportMaterials;

public class SupportMaterialService(ChiniseDbContext db) : ISupportMaterialService
{
    public async Task<IReadOnlyList<SupportMaterialSummary>> GetForEntryAsync(
        int entryId,
        int requestingEditorId,
        EditorRole requestingRole,
        CancellationToken ct = default) =>
        await db.EntrySupportMaterials
            .AsNoTracking()
            .Where(item => item.EntryId == entryId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.Id)
            .Select(item => new SupportMaterialSummary(
                item.Id,
                item.EntryId,
                item.Language,
                item.CommentText,
                item.CreatedAtUtc,
                item.AddedByEditorId == requestingEditorId ||
                requestingRole == EditorRole.ChiefEditor ||
                requestingRole == EditorRole.SuperAdmin))
            .ToListAsync(ct);

    public async Task<SupportMaterialSummary> AddAsync(
        int entryId,
        CreateSupportMaterialRequest request,
        int editorId,
        CancellationToken ct = default)
    {
        var language = SupportMaterialRules.NormalizeLanguage(request.Language)
            ?? throw new SupportMaterialValidationException(
                "Language must be Chinese, English, Russian, or Other.");
        var commentText = request.CommentText?.Trim();
        if (string.IsNullOrWhiteSpace(commentText))
        {
            throw new SupportMaterialValidationException("Comment text is required.");
        }

        if (commentText.Length > SupportMaterialRules.MaxCommentLength)
        {
            throw new SupportMaterialValidationException(
                $"Comment text must be {SupportMaterialRules.MaxCommentLength} characters or fewer.");
        }

        var entry = await db.Entries.FindAsync([entryId], ct)
            ?? throw new SupportMaterialValidationException($"Entry {entryId} does not exist.");

        var now = DateTime.UtcNow;
        var item = new EntrySupportMaterial
        {
            EntryId = entryId,
            AddedByEditorId = editorId,
            Language = language,
            CommentText = commentText,
            CreatedAtUtc = now,
        };
        db.EntrySupportMaterials.Add(item);

        entry.LastEditorEditorId = editorId;
        entry.UpdatedAtUtc = now;

        await db.SaveChangesAsync(ct);

        return new SupportMaterialSummary(
            item.Id,
            item.EntryId,
            item.Language,
            item.CommentText,
            item.CreatedAtUtc,
            true);
    }

    public async Task<bool> DeleteAsync(
        int entryId,
        int materialId,
        int requestingEditorId,
        EditorRole requestingRole,
        CancellationToken ct = default)
    {
        var item = await db.EntrySupportMaterials
            .Include(material => material.Entry)
            .SingleOrDefaultAsync(
                material => material.Id == materialId && material.EntryId == entryId,
                ct);
        if (item is null)
        {
            return false;
        }

        if (!EditorialNoteDeletionRules.CanDelete(
                item.AddedByEditorId,
                requestingEditorId,
                requestingRole))
        {
            throw new SupportMaterialAccessDeniedException();
        }

        var now = DateTime.UtcNow;
        db.EntrySupportMaterials.Remove(item);
        item.Entry.LastEditorEditorId = requestingEditorId;
        item.Entry.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);
        return true;
    }
}
