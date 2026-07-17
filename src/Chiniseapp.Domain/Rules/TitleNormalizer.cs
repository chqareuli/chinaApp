namespace Chiniseapp.Domain.Rules;

/// <summary>
/// Folds the Chinese fullwidth comma "，" to a regular "," so title search
/// (5.3 Comma variants) matches an entry regardless of which comma form its
/// lemma or the search query happens to use.
/// </summary>
public static class TitleNormalizer
{
    public static string Normalize(string title) => title.Replace('，', ',');
}
