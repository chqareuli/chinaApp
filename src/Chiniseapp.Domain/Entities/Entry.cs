using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Domain.Entities;

/// <summary>
/// Root of a dictionary article's segment tree. The article content itself lives
/// in <see cref="Segment"/> rows (EntryId, ParentSegmentId = null for roots);
/// this row carries workflow/authorship metadata and search-list fields.
/// </summary>
public class Entry
{
    public int Id { get; set; }

    /// <summary>Chinese headword (lemma), e.g. 为.</summary>
    public string Lemma { get; set; } = string.Empty;

    public string? Pinyin { get; set; }

    public EntryStatus Status { get; set; } = EntryStatus.NewEntry;

    /// <summary>
    /// App-maintained sort key for the editor list (Published=1 ... NewEntry=5),
    /// set by the status-change service on every transition rather than computed
    /// in the database, so the mapping stays in one testable place.
    /// </summary>
    public int StatusPriority { get; set; }

    public int CreatedByEditorId { get; set; }

    /// <summary>
    /// Locked once set: fixed when the entry first leaves NewEntry (or stays the
    /// assistant_editor who wrote it, if a supervisor is the one who promotes it).
    /// </summary>
    public int? MainAuthorEditorId { get; set; }

    public int LastEditorEditorId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Lemma normalized for the search dropdown: Chinese "，" and regular ","
    /// comma variants folded to one form so both are found by a single prefix
    /// match.
    /// </summary>
    public string SearchNormalizedTitle { get; set; } = string.Empty;

    public bool IsFlaggedForRepair { get; set; }

    public string? RepairNotes { get; set; }

    /// <summary>
    /// Untouched raw legacy comment blob for entries imported from the old
    /// database, kept immutable alongside the parsed <see cref="Comment"/> rows
    /// created during migration (M8).
    /// </summary>
    public string? LegacyRawCommentBlob { get; set; }

    public ICollection<Segment> Segments { get; } = new List<Segment>();
}
