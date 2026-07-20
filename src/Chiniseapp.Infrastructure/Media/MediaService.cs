using Chiniseapp.Application.Media;
using Chiniseapp.Application.Notifications;
using Chiniseapp.Application.Scoring;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Domain.Rules;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Chiniseapp.Infrastructure.Media;

public class MediaService(
    ChiniseDbContext db,
    IMediaFileStorage storage,
    IScoringService scoringService,
    INotificationService notificationService) : IMediaService
{
    public async Task<IReadOnlyList<MediaAssetItem>> ListForEntryAsync(int entryId, CancellationToken ct = default)
    {
        if (!await db.Entries.AnyAsync(entry => entry.Id == entryId, ct))
        {
            throw new MediaValidationException($"Entry {entryId} does not exist.");
        }

        return await QueryItems()
            .Where(item => item.EntryId == entryId)
            .OrderByDescending(item => item.UploadedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<MediaAssetItem> UploadAsync(
        int entryId,
        UploadEntryMediaCommand command,
        int currentEditorId,
        CancellationToken ct = default)
    {
        var metadataErrors = MediaUploadRules.ValidateMetadata(command.OriginalFilename, command.MimeType, command.Length);
        if (metadataErrors.Count > 0)
        {
            throw new MediaValidationException(string.Join(" ", metadataErrors));
        }

        var entry = await db.Entries.FindAsync([entryId], ct)
            ?? throw new MediaValidationException($"Entry {entryId} does not exist.");
        var actor = await db.Editors.FindAsync([currentEditorId], ct)
            ?? throw new MediaValidationException("Current editor not found.");
        if (!EntryContentRules.CanEdit(actor.Role, entry.Status))
        {
            throw new MediaAccessDeniedException(
                $"Your role cannot attach images while the entry is in '{entry.Status}' status.");
        }

        if (!string.IsNullOrWhiteSpace(command.Placement) && command.Placement.Trim().Length > 50)
        {
            throw new MediaValidationException("Placement must be 50 characters or fewer.");
        }

        await using var buffer = new MemoryStream((int)command.Length);
        await command.Content.CopyToAsync(buffer, ct);
        if (buffer.Length != command.Length || !MediaUploadRules.HasValidSignature(command.MimeType, buffer.GetBuffer().AsSpan(0, (int)buffer.Length)))
        {
            throw new MediaValidationException("The file content does not match its declared image type.");
        }

        var safeStorageName = $"{Guid.NewGuid():N}{MediaUploadRules.ExtensionFor(command.MimeType)}";
        buffer.Position = 0;
        await storage.SaveAsync(safeStorageName, buffer, ct);

        var now = DateTime.UtcNow;
        var asset = new MediaAsset
        {
            EntryId = entryId,
            OriginalFilename = Path.GetFileName(command.OriginalFilename),
            SafeStorageName = safeStorageName,
            FileType = MediaFileType.Image,
            MimeType = command.MimeType.ToLowerInvariant(),
            StorageUrl = $"/media/files/{safeStorageName}",
            Placement = string.IsNullOrWhiteSpace(command.Placement) ? null : command.Placement.Trim(),
            UploadedByEditorId = currentEditorId,
            UploadedAtUtc = now,
        };
        db.MediaAssets.Add(asset);
        entry.LastEditorEditorId = currentEditorId;
        entry.UpdatedAtUtc = now;
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            EntityType = nameof(Entry),
            EntityId = entryId,
            Action = AuditAction.ContentEdited,
            PerformedByEditorId = currentEditorId,
            PerformedAtUtc = now,
            Notes = $"Image attached: {asset.OriginalFilename}",
        });
        await scoringService.AwardForContentEditAsync(entry, currentEditorId, ct);
        await notificationService.NotifyEntryChangedAsync(entry, currentEditorId, nameof(AuditAction.ContentEdited), ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(safeStorageName, CancellationToken.None);
            throw;
        }

        return new MediaAssetItem(
            asset.Id, entryId, asset.OriginalFilename, asset.MimeType, asset.StorageUrl,
            asset.ThumbnailUrl, asset.Placement, currentEditorId, actor.DisplayName, now);
    }

    public async Task<bool> DeleteAsync(int id, int currentEditorId, CancellationToken ct = default)
    {
        var asset = await db.MediaAssets.FindAsync([id], ct);
        if (asset is null) return false;

        if (asset.EntryId is null)
        {
            throw new MediaValidationException("This media asset is not attached to an entry.");
        }
        var entry = await db.Entries.FindAsync([asset.EntryId.Value], ct)
            ?? throw new MediaValidationException($"Entry {asset.EntryId.Value} does not exist.");
        var actor = await db.Editors.FindAsync([currentEditorId], ct)
            ?? throw new MediaValidationException("Current editor not found.");
        if (!EntryContentRules.CanEdit(actor.Role, entry.Status))
        {
            throw new MediaAccessDeniedException(
                $"Your role cannot remove images while the entry is in '{entry.Status}' status.");
        }

        var now = DateTime.UtcNow;
        db.MediaAssets.Remove(asset);
        entry.LastEditorEditorId = currentEditorId;
        entry.UpdatedAtUtc = now;
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            EntityType = nameof(Entry),
            EntityId = entry.Id,
            Action = AuditAction.ContentEdited,
            PerformedByEditorId = currentEditorId,
            PerformedAtUtc = now,
            Notes = $"Image removed: {asset.OriginalFilename}",
        });
        await scoringService.AwardForContentEditAsync(entry, currentEditorId, ct);
        await notificationService.NotifyEntryChangedAsync(entry, currentEditorId, nameof(AuditAction.ContentEdited), ct);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(asset.SafeStorageName, ct);
        return true;
    }

    public async Task<MediaFileContent?> GetContentAsync(string safeStorageName, CancellationToken ct = default)
    {
        var asset = await db.MediaAssets.FirstOrDefaultAsync(item => item.SafeStorageName == safeStorageName, ct);
        if (asset is null) return null;
        var content = await storage.OpenReadAsync(asset.SafeStorageName, ct);
        return content is null ? null : new MediaFileContent(content, asset.MimeType, asset.OriginalFilename);
    }

    private IQueryable<MediaAssetItem> QueryItems() =>
        from asset in db.MediaAssets.AsNoTracking()
        join editor in db.Editors.AsNoTracking() on asset.UploadedByEditorId equals editor.Id
        select new MediaAssetItem(
            asset.Id, asset.EntryId!.Value, asset.OriginalFilename, asset.MimeType, asset.StorageUrl,
            asset.ThumbnailUrl, asset.Placement, asset.UploadedByEditorId, editor.DisplayName, asset.UploadedAtUtc);
}
