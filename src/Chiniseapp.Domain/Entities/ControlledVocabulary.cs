using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Domain.Entities;

/// <summary>
/// Admin-curated controlled list (POS ~15 codes, STYLE, DOMAIN, Abbr, Lang
/// markers). A reference table rather than an enum so these can be edited
/// without a deploy.
/// </summary>
public class ControlledVocabulary
{
    public int Id { get; set; }

    public VocabularyCategory Category { get; set; }

    /// <summary>Stable code, e.g. "名" for POS or "书" for STYLE. Unique per category.</summary>
    public string Code { get; set; } = string.Empty;

    public string DisplayZh { get; set; } = string.Empty;

    public string? DisplayKa { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
