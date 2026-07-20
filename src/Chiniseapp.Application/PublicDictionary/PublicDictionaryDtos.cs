using Chiniseapp.Application.Entries;

namespace Chiniseapp.Application.PublicDictionary;

public enum PublicSearchMode
{
    Word,
    Text,
}

public record PublicDictionarySearchItem(
    int Id,
    string Lemma,
    string? Pinyin,
    string Snippet);

public record PublicDictionaryEntry(
    int Id,
    string Lemma,
    string? Pinyin,
    DateTime UpdatedAtUtc,
    List<HomonymDto> Homonyms);

public record PublicDictionaryStats(int PublishedEntries);
