namespace Chiniseapp.Application.Entries;

public enum EntryListSort
{
    UpdatedDesc,
    UpdatedAsc,
    LemmaAsc,
    LemmaDesc,
    Status,
}

public sealed record CreateEntryRequest
{
    public string Lemma { get; init; } = string.Empty;

    public List<string> Pinyins { get; init; } = [];

    /// <summary>Backward-compatible input accepted from older clients.</summary>
    public string? Pinyin { get; init; }
}

public sealed record PinyinDto(int? Id, string Value, int Order);

public sealed record MarkerDto
{
    public int? Id { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

public sealed record CrossReferenceDto
{
    public int? Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
    public string? DisplayText { get; init; }
    public string Placement { get; init; } = "Standalone";
}

public sealed record ExampleDto
{
    public int? Id { get; init; }
    public int? ZhSegmentId { get; init; }
    public int? KaSegmentId { get; init; }
    public string Zh { get; init; } = string.Empty;
    public string Ka { get; init; } = string.Empty;
    public List<MarkerDto> Markers { get; init; } = [];
    public List<CrossReferenceDto> CrossReferences { get; init; } = [];
}

public sealed record SenseDto
{
    public int? Id { get; init; }
    public int? DefinitionId { get; init; }
    public int Number { get; init; }
    public string Definition { get; init; } = string.Empty;
    public List<MarkerDto> Markers { get; init; } = [];
    public List<CrossReferenceDto> CrossReferences { get; init; } = [];
    public List<ExampleDto> Examples { get; init; } = [];
}

public sealed record GramGroupDto
{
    public int? Id { get; init; }
    public int? PosSegmentId { get; init; }
    public int Number { get; init; }
    public string? Pos { get; init; }
    public List<MarkerDto> Markers { get; init; } = [];
    public List<CrossReferenceDto> CrossReferences { get; init; } = [];
    public List<SenseDto> Senses { get; init; } = [];
}

public sealed record DirectPosDto
{
    public int? Id { get; init; }
    public string Pos { get; init; } = string.Empty;
    public List<MarkerDto> Markers { get; init; } = [];
    public List<CrossReferenceDto> CrossReferences { get; init; } = [];
    public List<SenseDto> Senses { get; init; } = [];
}

public sealed record HomonymDto
{
    public int? Id { get; init; }
    public List<MarkerDto> Markers { get; init; } = [];
    public List<CrossReferenceDto> CrossReferences { get; init; } = [];
    public List<GramGroupDto> GramGroups { get; init; } = [];
}

public sealed record SaveEntryContentRequest
{
    public string? Lemma { get; init; }
    public List<PinyinDto> Pinyins { get; init; } = [];
    public List<HomonymDto> Homonyms { get; init; } = [];
    public List<GramGroupDto> GramGroups { get; init; } = [];
    public List<DirectPosDto> DirectPoses { get; init; } = [];
    public List<SenseDto> Senses { get; init; } = [];
    public uint RowVersion { get; init; }

    /// <summary>Backward-compatible input accepted from older clients.</summary>
    public string? Pinyin { get; init; }
}

public record EntryCapabilities(bool CanEditContent, IReadOnlyList<string> AllowedStatusTransitions);

public record EntryDetail(
    int Id,
    string Lemma,
    string? Pinyin,
    List<PinyinDto> Pinyins,
    string Status,
    string? MainAuthorDisplayName,
    string LastEditorDisplayName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    uint RowVersion,
    List<HomonymDto> Homonyms,
    List<GramGroupDto> GramGroups,
    List<DirectPosDto> DirectPoses,
    List<SenseDto> Senses,
    EntryCapabilities Capabilities);

public record EntrySummary(
    int Id,
    string Lemma,
    string? Pinyin,
    string Status,
    string? MainAuthorDisplayName,
    string LastEditorDisplayName,
    DateTime UpdatedAtUtc);

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public record EntryHistoryItem(
    int Id,
    string Action,
    int PerformedByEditorId,
    string PerformedByDisplayName,
    DateTime PerformedAtUtc,
    string? OldValue,
    string? NewValue,
    string? Notes);
