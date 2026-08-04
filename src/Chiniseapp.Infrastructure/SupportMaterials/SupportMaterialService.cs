using Chiniseapp.Application.SupportMaterials;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Rules;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Chiniseapp.Infrastructure.SupportMaterials;

public class SupportMaterialService(ChiniseDbContext db) : ISupportMaterialService
{
    public async Task<IReadOnlyList<SupportMaterialSummary>> GetForEntryAsync(
        int entryId,
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
                item.CreatedAtUtc))
            .ToListAsync(ct);

    public async Task<SupportMaterialSummary> AddAsync(
        int entryId,
        CreateSupportMaterialRequest request,
        int editorId,
        CancellationToken ct = default)
    {
        var language = SupportMaterialRules.NormalizeLanguage(request.Language)
            ?? throw new SupportMaterialValidationException(
                "Language must be English, Chinese, or Russian.");
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
            item.CreatedAtUtc);
    }
}
