using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chiniseapp.Application.Entries;

public record CreateEntryRequest(string Lemma, string? Pinyin)
{
    public List<string>? Pinyins { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record MarkerDto(int? Id, string Category, string Value);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CrossReferenceDto(int? Id, string Type, string Label, string Target,
    int? TargetEntryId, string? TargetEntryLemma, string? DisplayText, string Placement);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record FormItemDto(int? Id, string Value, int Order);
public record LexicalForms(List<FormItemDto> Pinyins, List<FormItemDto> ComplexForms);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record ExampleDto(string Zh, string Ka)
{
    public int? Id { get; init; }
    public int? ZhSegmentId { get; init; }
    public int? KaSegmentId { get; init; }
    public List<MarkerDto> Markers { get; init; } = [];
    public List<CrossReferenceDto> CrossReferences { get; init; } = [];
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record SenseDto(int Number, string Definition, List<ExampleDto> Examples)
{
    public int? Id { get; init; }
    public int? DefinitionId { get; init; }
    public List<MarkerDto> Markers { get; init; } = [];
    public List<CrossReferenceDto> CrossReferences { get; init; } = [];
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record GramGroupDto
{
    public int? Id { get; init; }
    public int? PosSegmentId { get; init; }
    public int Number { get; init; }
    public string? Pos { get; init; }
    public List<MarkerDto> Markers { get; init; } = [];
    public List<CrossReferenceDto> CrossReferences { get; init; } = [];
    public List<SenseDto> Senses { get; init; } = [];
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record DirectPosDto
{
    public int? Id { get; init; }
    public string Pos { get; init; } = "";
    public List<MarkerDto> Markers { get; init; } = [];
    public List<CrossReferenceDto> CrossReferences { get; init; } = [];
    public List<SenseDto> Senses { get; init; } = [];
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record HomonymDto(List<SenseDto>? Senses)
{
    public List<SenseDto> DirectSenses { get; init; } = [];
    public int? Id { get; init; }
    public string Text { get; init; } = "";
    public List<MarkerDto> Markers { get; init; } = [];
    public List<CrossReferenceDto> CrossReferences { get; init; } = [];
    public List<GramGroupDto>? GramGroups { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record SaveEntryContentRequest(string? Lemma, string? Pinyin, List<HomonymDto> Homonyms, uint RowVersion)
{
    public List<FormItemDto>? Pinyins { get; init; }
    public List<FormItemDto>? ComplexForms { get; init; }
    public List<GramGroupDto> GramGroups { get; init; } = [];
    public List<DirectPosDto> DirectPoses { get; init; } = [];
    public List<SenseDto> Senses { get; init; } = [];
}

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
    List<HomonymDto> Homonyms,
    EntryCapabilities Capabilities)
{
    public List<FormItemDto> Pinyins { get; init; } = [];
    public List<FormItemDto> ComplexForms { get; init; } = [];
    public List<GramGroupDto> GramGroups { get; init; } = [];
    public List<DirectPosDto> DirectPoses { get; init; } = [];
    public List<SenseDto> Senses { get; init; } = [];
}

public record EntryCapabilities(bool CanEditContent, IReadOnlyList<string> AllowedStatusTransitions);

public record EntrySummary(
    int Id,
    string Lemma,
    string? Pinyin,
    string Status,
    string? MainAuthorDisplayName,
    string LastEditorDisplayName,
    DateTime UpdatedAtUtc);

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
