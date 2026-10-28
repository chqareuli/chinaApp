using System.Text.Json;
using Chiniseapp.Application.Entries;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Infrastructure.Entries;

/// <summary>Maps the editor contract onto segment rows without replacing surviving identities.</summary>
public sealed class EntryTreeMapper(int entryId, IReadOnlyList<Segment> existing, DateTime now)
{
    private readonly Dictionary<int, Segment> byId = existing.ToDictionary(s => s.Id);
    private readonly HashSet<int> used = [];
    public List<Segment> Nodes { get; } = [];
    public IEnumerable<Segment> Removed => existing.Where(s => !used.Contains(s.Id));
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private Segment Node(int? id, SegmentType type, Segment? parent, int order, string? value = null)
    {
        Segment node;
        if (id is int key)
        {
            if (!byId.TryGetValue(key, out node!) || node.EntryId != entryId || node.SegmentType != type || !used.Add(key))
                throw new EntryValidationException("Invalid, duplicate, or foreign segment identity. Reload the entry and retry.");
        }
        else node = new Segment { EntryId = entryId, SegmentType = type, CreatedAtUtc = now };
        node.Parent = parent;
        node.ParentSegmentId = parent is { Id: > 0 } ? parent.Id : null;
        node.OrderIndex = order;
        node.Value = value;
        node.UpdatedAtUtc = now;
        Nodes.Add(node);
        if (Nodes.Count > 10000) throw new EntryValidationException("An entry may contain at most 10,000 segments.");
        return node;
    }

    public void Apply(SaveEntryContentRequest request)
    {
        foreach (var (h, i) in (request.Homonyms ?? []).Select((x, i) => (x, i)))
        {
            var node = Node(h.Id, SegmentType.Homonym, null, i, h.Text);
            node.Attributes = h.PinyinId.HasValue ? JsonSerializer.Serialize(new { pinyinId = h.PinyinId.Value }, Json) : null;
            Decorations(node, h.Markers, h.CrossReferences);
            var groups = h.GramGroups ?? [new GramGroupDto { Senses = h.Senses ?? [] }];
            Groups(node, groups);
            Senses(node,h.DirectSenses);
        }
        Groups(null, request.GramGroups);
        foreach (var (p, i) in request.DirectPoses.Select((x, i) => (x, i)))
        {
            var node = Node(p.Id, SegmentType.Pos, null, i, p.Pos);
            Decorations(node, p.Markers, p.CrossReferences);
            Senses(node, p.Senses);
        }
        Senses(null, request.Senses);
    }

    private void Groups(Segment? parent, List<GramGroupDto> groups)
    {
        foreach (var (g, i) in groups.Select((x, i) => (x, i)))
        {
            var node = Node(g.Id, SegmentType.GramGrp, parent, i);
            node.Number = i + 1;
            if (!string.IsNullOrEmpty(g.Pos)) Node(g.PosSegmentId, SegmentType.Pos, node, 0, g.Pos);
            Decorations(node, g.Markers, g.CrossReferences);
            Senses(node, g.Senses);
        }
    }

    private void Senses(Segment? parent, List<SenseDto> senses)
    {
        foreach (var (s, i) in senses.Select((x, i) => (x, i)))
        {
            var node = Node(s.Id, SegmentType.Sense, parent, i);
            node.Number = i + 1;
            Node(s.DefinitionId, SegmentType.Definition, node, 0, s.Definition ?? "");
            Decorations(node, s.Markers, s.CrossReferences);
            foreach (var (e, j) in (s.Examples ?? []).Select((x, i) => (x, i)))
            {
                var example = Node(e.Id, SegmentType.Example, node, j + 1);
                Node(e.ZhSegmentId, SegmentType.ZhSegment, example, 0, e.Zh ?? "");
                Node(e.KaSegmentId, SegmentType.KaSegment, example, 1, e.Ka ?? "");
                Decorations(example, e.Markers, e.CrossReferences);
            }
        }
    }

    private void Decorations(Segment parent, List<MarkerDto> markers, List<CrossReferenceDto> references)
    {
        foreach (var (m, i) in markers.Select((x, i) => (x, i)))
        {
            if (!Enum.TryParse<SegmentType>(m.Category, out var type) || type is not (SegmentType.Style or SegmentType.Domain or SegmentType.Abbr or SegmentType.Lang))
                throw new EntryValidationException("Unknown marker category.");
            var node = Node(m.Id, type, parent, i, m.Value);
            node.Placement = m.TextField is null ? Placement.Attached : Placement.Inline;
            node.Attributes = m.TextField is null ? null : JsonSerializer.Serialize(m with { Id = null }, Json);
        }
        foreach (var (r, i) in references.Select((x, i) => (x, i)))
        {
            if (r.Type is not ("see" or "seeAlso" or "equivalent" or "antonym" or "illustration") ||
                !Enum.TryParse<Placement>(r.Placement, out var placement) || !Enum.IsDefined(placement))
                throw new EntryValidationException("Unknown cross-reference type or placement.");
            var node = Node(r.Id, SegmentType.Xr, parent, i, r.Target);
            node.Placement = placement;
            node.Attributes = JsonSerializer.Serialize(r with { Id = null }, Json);
        }
    }

    public static SaveEntryContentRequest Read(IReadOnlyList<Segment> segments)
    {
        var children = segments.ToLookup(s => s.ParentSegmentId);
        List<Segment> Children(int? id, SegmentType type) => children[id].Where(s => s.SegmentType == type).OrderBy(s => s.OrderIndex).ThenBy(s => s.Id).ToList();
        List<MarkerDto> Markers(int id) => children[id].Where(s => s.SegmentType is SegmentType.Style or SegmentType.Domain or SegmentType.Abbr or SegmentType.Lang)
            .OrderBy(s => s.OrderIndex).Select(s => s.Attributes is not null && JsonSerializer.Deserialize<MarkerDto>(s.Attributes, Json) is { } marker
                ? marker with { Id = s.Id } : new MarkerDto(s.Id, s.SegmentType.ToString(), s.Value ?? "")).ToList();
        List<CrossReferenceDto> References(int id) => Children(id, SegmentType.Xr).Select(s =>
            (s.Attributes is not null ? ReadReference(s.Attributes) : null) is { } r
                ? r with { Id = s.Id, Placement = s.Placement?.ToString() ?? "Attached" }
                : new CrossReferenceDto(s.Id, "see", "", s.Value ?? "", null, null, null, s.Placement?.ToString() ?? "Attached")).ToList();
        List<SenseDto> Senses(int? id) => Children(id, SegmentType.Sense).Select((s, i) =>
        {
            var definition = Children(s.Id, SegmentType.Definition).FirstOrDefault();
            return new SenseDto(i + 1, definition?.Value ?? "", Children(s.Id, SegmentType.Example).Select(e =>
            {
                var zh = Children(e.Id, SegmentType.ZhSegment).FirstOrDefault();
                var ka = Children(e.Id, SegmentType.KaSegment).FirstOrDefault();
                return new ExampleDto(zh?.Value ?? "", ka?.Value ?? "")
                { Id = e.Id, ZhSegmentId = zh?.Id, KaSegmentId = ka?.Id, Markers = Markers(e.Id), CrossReferences = References(e.Id) };
            }).ToList()) { Id = s.Id, DefinitionId = definition?.Id, Markers = Markers(s.Id), CrossReferences = References(s.Id) };
        }).ToList();
        List<GramGroupDto> Groups(int? id) => Children(id, SegmentType.GramGrp).Select((g, i) =>
        {
            var pos = Children(g.Id, SegmentType.Pos).FirstOrDefault();
            return new GramGroupDto { Id = g.Id, Number = i + 1, PosSegmentId = pos?.Id, Pos = pos?.Value,
                Markers = Markers(g.Id), CrossReferences = References(g.Id), Senses = Senses(g.Id) };
        }).ToList();
        return new SaveEntryContentRequest(null, null, Children(null, SegmentType.Homonym).Select(h =>
        {
            var groups = Groups(h.Id);
            return new HomonymDto(groups.SelectMany(g => g.Senses).ToList())
                { Id = h.Id, PinyinId = h.Attributes is not null ? JsonSerializer.Deserialize<HomonymDto>(h.Attributes, Json)?.PinyinId : null, Text = h.Value ?? "", GramGroups = groups, DirectSenses=Senses(h.Id), Markers = Markers(h.Id), CrossReferences = References(h.Id) };
        }).ToList(), 0)
        {
            GramGroups = Groups(null), Senses = Senses(null),
            DirectPoses = Children(null, SegmentType.Pos).Select(p => new DirectPosDto
                { Id = p.Id, Pos = p.Value ?? "", Senses = Senses(p.Id), Markers = Markers(p.Id), CrossReferences = References(p.Id) }).ToList()
        };
    }

    private static CrossReferenceDto? ReadReference(string attributes)
    {
        var data = System.Text.Json.Nodes.JsonNode.Parse(attributes)!.AsObject();
        if (data.Remove("xrType", out var legacyType) && !data.ContainsKey("type")) data["type"] = legacyType;
        return data.Deserialize<CrossReferenceDto>(Json);
    }

    public static LexicalForms ReadForms(Entry entry) => entry.LexicalForms is not null
        ? JsonSerializer.Deserialize<LexicalForms>(entry.LexicalForms, Json) ?? new([], [])
        : new(string.IsNullOrWhiteSpace(entry.Pinyin) ? [] : [new FormItemDto(1, entry.Pinyin, 0)], []);

    public static SaveEntryContentRequest LinkPronunciations(SaveEntryContentRequest request, LexicalForms forms)
    {
        return request with { Homonyms = request.Homonyms.Select(h =>
        {
            if (h.PinyinId is null && h.PinyinOrder is null) return h with { Text = h.Text is null ? string.Empty : System.Text.RegularExpressions.Regex.Replace(h.Text.Normalize().Trim(), @"(?:\s+\d+)+$", "").Trim() };
            var pinyin = h.PinyinId is int id ? forms.Pinyins.FirstOrDefault(p => p.Id == id)
                : forms.Pinyins.FirstOrDefault(p => p.Order == h.PinyinOrder);
            if (pinyin is null) throw new EntryValidationException("A homograph's linked pronunciation is missing. Select another pronunciation or unlink the homograph before removing it.");
            return h with { PinyinId = pinyin.Id, PinyinOrder = null, Text = pinyin.Value };
        }).ToList() };
    }

    public static void SaveForms(Entry entry, List<FormItemDto>? pinyins, List<FormItemDto>? forms)
    {
        var previous = ReadForms(entry);
        List<FormItemDto> Normalize(List<FormItemDto>? items, List<FormItemDto> old, int maximum)
        {
            if (items is null) return old;
            if (items.Count > maximum) throw new EntryValidationException($"At most {maximum} values are allowed.");
            var next = old.Select(f => f.Id ?? 0).DefaultIfEmpty().Max() + 1;
            var seen = new HashSet<int>();
            var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return items.Select((item, index) =>
            {
                var value = item.Value?.Trim() ?? "";
                if (value.Length is 0 or > 255 || !values.Add(value)) throw new EntryValidationException("Pronunciations and traditional forms must be nonempty, unique, and at most 255 characters.");
                var id = item.Id ?? next++;
                if ((item.Id.HasValue && !old.Any(f => f.Id == id)) || !seen.Add(id)) throw new EntryValidationException("Invalid pronunciation or traditional-form identity.");
                return new FormItemDto(id, value, index);
            }).ToList();
        }
        var result = new LexicalForms(Normalize(pinyins, previous.Pinyins, 5), Normalize(forms, previous.ComplexForms, 3));
        entry.LexicalForms = JsonSerializer.Serialize(result, Json);
        entry.Pinyin = result.Pinyins.FirstOrDefault()?.Value;
    }
}
