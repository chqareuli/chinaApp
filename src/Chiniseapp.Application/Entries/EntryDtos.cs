namespace Chiniseapp.Application.Entries;

public record CreateEntryRequest(string Lemma, string? Pinyin);

public record ExampleDto(string Zh, string Ka);

public record SenseDto(int Number, string Definition, List<ExampleDto> Examples);

public record HomonymDto(List<SenseDto> Senses);

/// <summary>
/// Full-replace content save: existing segments for the entry are deleted and
/// re-created from this shape. Simple and robust for Stage 1's small articles;
/// a real partial/diff-based save (preserving segment ids across saves, needed
/// once Stage 2 lets Comments/XR target specific segments) is a Stage 2 concern.
/// </summary>
public record SaveEntryContentRequest(string? Lemma, string? Pinyin, List<HomonymDto> Homonyms, uint RowVersion);

public record EntryDetail(
    int Id,
    string Lemma,
    string? Pinyin,
    string Status,
    string? MainAuthorDisplayName,
    string LastEditorDisplayName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    uint RowVersion,
    List<HomonymDto> Homonyms);

public record EntrySummary(
    int Id,
    string Lemma,
    string? Pinyin,
    string Status,
    string? MainAuthorDisplayName,
    string LastEditorDisplayName,
    DateTime UpdatedAtUtc);

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
