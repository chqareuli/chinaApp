namespace Chiniseapp.Domain.Entities;

/// <summary>
/// One ordered Pinyin pronunciation belonging to an entry. Pronunciations are
/// first-class rows so the editor can add, edit, remove and reorder them
/// without encoding a list into one text column.
/// </summary>
public class EntryPronunciation
{
    public int Id { get; set; }

    public int EntryId { get; set; }

    public Entry Entry { get; set; } = null!;

    public string Value { get; set; } = string.Empty;

    public string NormalizedValue { get; set; } = string.Empty;

    public int OrderIndex { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
