using System.Text.Json;
using Chiniseapp.Application.Entries;
using Chiniseapp.Domain.Entities;

namespace Chiniseapp.Infrastructure.Entries;

public static class EntryMediaRules
{
    public const string DetachedPrefix = "detached:";
    public static bool IsDetached(MediaAsset media) => media.Placement?.StartsWith(DetachedPrefix, StringComparison.Ordinal) == true;

    public static bool HasMissingTarget(MediaAsset media, IEnumerable<Segment> segments)
    {
        if (IsDetached(media)) return false;
        var parents = new[] { PlacementParent(media.Placement), media.InsertedIntoSegmentId }.OfType<int>().Distinct().ToList();
        var current = segments.Where(s => s.EntryId == media.EntryId).Select(s => s.Id).ToHashSet();
        return parents.Count > 0 && parents.All(id => !current.Contains(id));
    }

    public static void DetachMissingTarget(MediaAsset media, IEnumerable<Segment> segments, int editorId, DateTime now)
    {
        if (IsDetached(media)) return;
        if (!HasMissingTarget(media, segments))
            throw new EntryValidationException($"Entry {media.EntryId}, media {media.Id}: this image has a current or unspecified target. Its link was preserved. · 此图片的目标仍有效或未指定，链接已保留。");
        media.Placement = DetachedPrefix + JsonSerializer.Serialize(new {
            reason = "missingMediaTarget", originalPlacement = media.Placement,
            originalSegmentId = media.InsertedIntoSegmentId, detachedAtUtc = now, detachedByEditorId = editorId
        });
        media.InsertedIntoSegmentId = null;
    }
    // Placement carries a second parent reference, including legacy links whose FK was nulled.
    public static int? PlacementParent(string? placement)
    {
        if (placement?.StartsWith("inline:", StringComparison.Ordinal) == true)
        {
            try
            {
                using var document = JsonDocument.Parse(placement[7..]);
                var root = document.RootElement;
                return root.TryGetProperty("nodeId", out var node) && node.TryGetInt32(out var id) && id > 0 ? id : null;
            }
            catch (Exception error) when (error is JsonException or InvalidOperationException) { return null; }
        }
        var parts = placement?.Split(':');
        return parts is { Length: 2 } && parts[0] is "sense" or "example" && int.TryParse(parts[1], out var key) && key > 0 ? key : null;
    }

    public static void EnsureSegmentsCanBeRemoved(int entryId, IReadOnlySet<int> removedIds, IEnumerable<MediaAsset> media)
    {
        if (removedIds.Count == 0) return;
        foreach (var asset in media.Where(m => m.EntryId == entryId && !IsDetached(m)).OrderBy(m => m.Id))
        {
            var target = new[] { asset.InsertedIntoSegmentId, PlacementParent(asset.Placement) }
                .FirstOrDefault(id => id.HasValue && removedIds.Contains(id.Value));
            if (target is int id)
                throw new EntryValidationException($"Entry {entryId}, media {asset.Id}, segment {id}: reattach the image or remove its media link before deleting or replacing this block. · 删除或替换此内容块前，请重新关联图片或移除其图片链接。");
        }
    }
}
