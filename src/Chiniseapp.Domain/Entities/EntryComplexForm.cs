namespace Chiniseapp.Domain.Entities;

/// <summary>
/// One ordered complex written form belonging to an entry. These values are
/// stored separately from the lemma and Pinyin so editors can add, edit and
/// remove several Chinese-script variants independently.
/// </summary>
public class EntryComplexForm
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
