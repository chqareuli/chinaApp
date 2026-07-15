using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Domain.Entities;

/// <summary>
/// Internal editorial note tied to an entry (Stage 1) or a specific segment
/// (Stage 2, via <see cref="TargetSegmentId"/>). Never exported to the public
/// dictionary content.
/// </summary>
public class Comment
{
    public int Id { get; set; }

    public int EntryId { get; set; }

    /// <summary>Always null in Stage 1; segment-level comments arrive in Stage 2.</summary>
    public int? TargetSegmentId { get; set; }

    public int AuthorEditorId { get; set; }

    public string CommentText { get; set; } = string.Empty;

    public CommentStatus Status { get; set; } = CommentStatus.Open;

    /// <summary>
    /// Set only on the single legacy-imported row per entry: the untouched raw
    /// comment blob from the old database, using informal markers
    /// (&amp;Author&amp; shifts, #...# Georgian-editor notes). Never modified.
    /// </summary>
    public string? OriginalRawComment { get; set; }

    /// <summary>
    /// jsonb best-effort parse of <see cref="OriginalRawComment"/>:
    /// [{"authorMarker":"...","content":[{"type":"text","text":"..."},
    /// {"type":"editor_note_ka","text":"..."}]}]. Null for ordinary post-migration
    /// comments.
    /// </summary>
    public string? ParsedCommentParts { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
