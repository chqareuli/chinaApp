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

public record PublicDictionaryCrossReference(
    string Type,
    string Label,
    string Target,
    string? DisplayText,
    string Placement,
    int? TargetEntryId,
    string? TargetEntryLemma);

public record PublicDictionaryExample(
    string Zh,
    string Ka,
    List<PublicDictionaryCrossReference> CrossReferences);

public record PublicDictionarySense(
    int Number,
    string Definition,
    List<PublicDictionaryCrossReference> CrossReferences,
    List<PublicDictionaryExample> Examples);

public record PublicDictionaryGramGroup(
    int Number,
    string? Pos,
    List<PublicDictionaryCrossReference> CrossReferences,
    List<PublicDictionarySense> Senses);

public record PublicDictionaryHomonym(
    string? Text,
    List<PublicDictionaryCrossReference> CrossReferences,
    List<PublicDictionaryGramGroup> GramGroups);

public record PublicDictionaryDirectPos(
    string Pos,
    List<PublicDictionaryCrossReference> CrossReferences,
    List<PublicDictionarySense> Senses);

public record PublicDictionaryEntry(
    int Id,
    string Lemma,
    string? Pinyin,
    List<string> Pinyins,
    List<string> ComplexForms,
    DateTime UpdatedAtUtc,
    List<PublicDictionaryHomonym> Homonyms,
    List<PublicDictionaryGramGroup> GramGroups,
    List<PublicDictionaryDirectPos> DirectPoses,
    List<PublicDictionarySense> Senses);

public record PublicDictionaryStats(int PublishedEntries);
