using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Domain.Entities;

/// <summary>
/// Stage 2+ concern (media insertion into entry segments, dictionary library);
/// the entity is reserved now so the schema doesn't need retrofitting later.
/// Actual files live in external/cloud storage — StorageUrl/ThumbnailUrl are
/// stable references, not local paths.
/// </summary>
public class MediaAsset
{
    public int Id { get; set; }

    /// <summary>Segment the media is inserted into, if any.</summary>
    public int? InsertedIntoSegmentId { get; set; }

    /// <summary>Unicode-safe original filename (Chinese/Georgian/English/digits allowed).</summary>
    public string OriginalFilename { get; set; } = string.Empty;

    /// <summary>Safe internal storage filename/key, decoupled from the original.</summary>
    public string SafeStorageName { get; set; } = string.Empty;

    public MediaFileType FileType { get; set; }

    public string MimeType { get; set; } = string.Empty;

    public string StorageUrl { get; set; } = string.Empty;

    public string? ThumbnailUrl { get; set; }

    public string? Placement { get; set; }

    public int UploadedByEditorId { get; set; }

    public DateTime UploadedAtUtc { get; set; }
}
