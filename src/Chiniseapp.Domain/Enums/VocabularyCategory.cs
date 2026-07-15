namespace Chiniseapp.Domain.Enums;

/// <summary>
/// Category of an admin-curated <see cref="Entities.ControlledVocabulary"/> entry
/// (POS ~15 codes, STYLE, DOMAIN, Abbr, Lang markers). Larger/editable vocabularies
/// live in a reference table instead of a native enum so the product owner can
/// maintain them without a deploy.
/// </summary>
public enum VocabularyCategory
{
    Pos,
    Style,
    Domain,
    Abbr,
    Lang,
}
