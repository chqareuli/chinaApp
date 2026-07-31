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

public record PublicDictionaryExample(string Zh, string Ka);

public record PublicDictionarySense(int Number, string Definition, List<PublicDictionaryExample> Examples);

public record PublicDictionaryGramGroup(int Number, string? Pos, List<PublicDictionarySense> Senses);

public record PublicDictionaryHomonym(List<PublicDictionaryGramGroup> GramGroups);

public record PublicDictionaryDirectPos(string Pos, List<PublicDictionarySense> Senses);

public record PublicDictionaryEntry(
    int Id,
    string Lemma,
    string? Pinyin,
    List<string> Pinyins,
    DateTime UpdatedAtUtc,
    List<PublicDictionaryHomonym> Homonyms,
    List<PublicDictionaryGramGroup> GramGroups,
    List<PublicDictionaryDirectPos> DirectPoses,
    List<PublicDictionarySense> Senses);

public record PublicDictionaryStats(int PublishedEntries);
