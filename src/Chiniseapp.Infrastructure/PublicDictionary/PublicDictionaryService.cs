using System.Text.Json;
using Chiniseapp.Application.PublicDictionary;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Domain.Rules;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Chiniseapp.Infrastructure.PublicDictionary;

public class PublicDictionaryService(ChiniseDbContext db) : IPublicDictionaryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyDictionary<string, string> CrossReferenceLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["see"] = "იხ.",
            ["seeAlso"] = "ასევე.",
            ["equivalent"] = "იგივეა,რაც",
            ["antonym"] = "იხ. ანტ.",
            ["illustration"] = "იხ. სურ.",
        };

    public async Task<IReadOnlyList<PublicDictionarySearchItem>> SearchAsync(
        string query,
        PublicSearchMode mode,
        int limit,
        CancellationToken ct = default)
    {
        var term = query.Trim();
        if (term.Length == 0)
        {
            return [];
        }

        limit = Math.Clamp(limit, 1, 50);
        var entries = db.Entries
            .AsNoTracking()
            .Where(entry => entry.Status == EntryStatus.Published);

        List<Entry> matches;
        if (mode == PublicSearchMode.Word)
        {
            var normalized = TitleNormalizer.Normalize(term);
            var pinyinPattern = EscapeLikePattern(term) + "%";
            matches = await entries
                .Where(entry =>
                    EF.Functions.Like(entry.SearchNormalizedTitle, normalized + "%") ||
                    (entry.Pinyin != null && EF.Functions.ILike(entry.Pinyin, pinyinPattern, "\\")) ||
                    db.EntryPronunciations.Any(item =>
                        item.EntryId == entry.Id && EF.Functions.ILike(item.Value, pinyinPattern, "\\")) ||
                    db.EntryComplexForms.Any(item =>
                        item.EntryId == entry.Id && EF.Functions.Like(item.NormalizedValue, normalized + "%")))
                .OrderBy(entry => entry.Lemma.Length)
                .ThenBy(entry => entry.Lemma)
                .Take(limit)
                .ToListAsync(ct);
        }
        else
        {
            var pattern = "%" + EscapeLikePattern(term) + "%";
            matches = await entries
                .Where(entry =>
                    EF.Functions.ILike(entry.Lemma, pattern, "\\") ||
                    (entry.Pinyin != null && EF.Functions.ILike(entry.Pinyin, pattern, "\\")) ||
                    db.EntryPronunciations.Any(item =>
                        item.EntryId == entry.Id && EF.Functions.ILike(item.Value, pattern, "\\")) ||
                    db.EntryComplexForms.Any(item =>
                        item.EntryId == entry.Id && EF.Functions.ILike(item.Value, pattern, "\\")) ||
                    db.Segments.Any(segment =>
                        segment.EntryId == entry.Id &&
                        segment.Value != null &&
                        EF.Functions.ILike(segment.Value, pattern, "\\")))
                .OrderByDescending(entry => entry.UpdatedAtUtc)
                .Take(limit)
                .ToListAsync(ct);
        }

        if (matches.Count == 0)
        {
            return [];
        }

        var snippets = mode == PublicSearchMode.Text
            ? await LoadSnippetsAsync(matches.Select(entry => entry.Id).ToList(), term, ct)
            : new Dictionary<int, string>();

        return matches.Select(entry => new PublicDictionarySearchItem(
                entry.Id,
                entry.Lemma,
                entry.Pinyin,
                snippets.GetValueOrDefault(entry.Id, string.Empty)))
            .ToList();
    }

    public async Task<PublicDictionaryEntry?> GetEntryAsync(int id, CancellationToken ct = default)
    {
        var entry = await db.Entries
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.Status == EntryStatus.Published, ct);
        if (entry is null)
        {
            return null;
        }

        var content = await LoadContentAsync(entry.Id, ct);
        return new PublicDictionaryEntry(
            entry.Id,
            entry.Lemma,
            entry.Pinyin,
            await db.EntryPronunciations
                .AsNoTracking()
                .Where(item => item.EntryId == entry.Id)
                .OrderBy(item => item.OrderIndex)
                .Select(item => item.Value)
                .ToListAsync(ct),
            await db.EntryComplexForms
                .AsNoTracking()
                .Where(item => item.EntryId == entry.Id)
                .OrderBy(item => item.OrderIndex)
                .Select(item => item.Value)
                .ToListAsync(ct),
            entry.UpdatedAtUtc,
            content.Homonyms,
            content.GramGroups,
            content.DirectPoses,
            content.Senses);
    }

    public async Task<PublicDictionaryStats> GetStatsAsync(CancellationToken ct = default) =>
        new(await db.Entries.CountAsync(entry => entry.Status == EntryStatus.Published, ct));

    private async Task<Dictionary<int, string>> LoadSnippetsAsync(
        List<int> entryIds,
        string term,
        CancellationToken ct)
    {
        var values = await db.Segments
            .AsNoTracking()
            .Where(segment => entryIds.Contains(segment.EntryId) && segment.Value != null)
            .Select(segment => new { segment.EntryId, segment.Value })
            .ToListAsync(ct);

        return values
            .Where(item => item.Value!.Contains(term, StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.EntryId)
            .ToDictionary(group => group.Key, group => BuildSnippet(group.First().Value!, term));
    }

    private async Task<PublicContentGraph> LoadContentAsync(int entryId, CancellationToken ct)
    {
        var segments = await db.Segments
            .AsNoTracking()
            .Where(segment => segment.EntryId == entryId)
            .OrderBy(segment => segment.OrderIndex)
            .ToListAsync(ct);
        var crossReferenceAttributes = segments
            .Where(segment => segment.SegmentType == SegmentType.Xr)
            .ToDictionary(segment => segment.Id, ParseXrAttributes);
        var linkedTargetIds = crossReferenceAttributes.Values
            .Where(attributes => attributes?.TargetEntryId is not null)
            .Select(attributes => attributes!.TargetEntryId!.Value)
            .Distinct()
            .ToList();
        var normalizedTargets = crossReferenceAttributes.Values
            .Where(attributes => attributes is not null)
            .Select(attributes => TitleNormalizer.Normalize(attributes!.Target.Trim()))
            .Where(target => target.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var targetEntries = await db.Entries
            .AsNoTracking()
            .Where(item =>
                item.Id != entryId &&
                item.Status == EntryStatus.Published &&
                (linkedTargetIds.Contains(item.Id) || normalizedTargets.Contains(item.SearchNormalizedTitle)))
            .Select(item => new CrossReferenceTarget(item.Id, item.Lemma, item.SearchNormalizedTitle))
            .ToListAsync(ct);
        var targetEntriesById = targetEntries.ToDictionary(item => item.Id);
        var targetEntriesByNormalized = targetEntries
            .GroupBy(item => item.NormalizedLemma, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item.Id).First(),
                StringComparer.Ordinal);

        var byParent = segments
            .Where(segment => segment.ParentSegmentId.HasValue)
            .GroupBy(segment => segment.ParentSegmentId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderBy(segment => segment.OrderIndex).ToList());

        IEnumerable<Segment> ChildrenOf(int? parentId) => parentId is null
            ? segments.Where(segment => segment.ParentSegmentId is null)
            : byParent.TryGetValue(parentId.Value, out var children) ? children : [];

        List<Segment> Children(int? parentId, SegmentType type) =>
            ChildrenOf(parentId).Where(segment => segment.SegmentType == type).ToList();

        List<PublicDictionaryCrossReference> CrossReferences(int? parentId)
        {
            if (parentId is null)
            {
                return [];
            }

            return Children(parentId, SegmentType.Xr)
                .Select(segment =>
                {
                    var attributes = crossReferenceAttributes.GetValueOrDefault(segment.Id);
                    CrossReferenceTarget? targetEntry = null;
                    if (attributes?.TargetEntryId is int targetEntryId)
                    {
                        targetEntriesById.TryGetValue(targetEntryId, out targetEntry);
                    }
                    if (targetEntry is null && attributes is not null)
                    {
                        targetEntriesByNormalized.TryGetValue(
                            TitleNormalizer.Normalize(attributes.Target.Trim()),
                            out targetEntry);
                    }

                    var type = attributes?.XrType ?? "see";
                    var label = attributes?.Label is { Length: > 0 }
                        ? attributes.Label
                        : CrossReferenceLabels.GetValueOrDefault(type, CrossReferenceLabels["see"]);
                    var target = attributes?.Target ?? segment.Value ?? string.Empty;
                    return new PublicDictionaryCrossReference(
                        type,
                        label,
                        target,
                        attributes?.DisplayText,
                        (segment.Placement ?? Placement.Standalone).ToString(),
                        targetEntry?.Id,
                        targetEntry?.Lemma);
                })
                .ToList();
        }

        PublicDictionarySense MapSense(Segment sense, int fallbackNumber)
        {
            var definition = Children(sense.Id, SegmentType.Definition).FirstOrDefault();
            var examples = Children(sense.Id, SegmentType.Example)
                .Select(example =>
                {
                    var zh = Children(example.Id, SegmentType.ZhSegment).FirstOrDefault();
                    var ka = Children(example.Id, SegmentType.KaSegment).FirstOrDefault();
                    return new PublicDictionaryExample(
                        zh?.Value ?? string.Empty,
                        ka?.Value ?? string.Empty,
                        CrossReferences(ka?.Id));
                })
                .ToList();
            return new PublicDictionarySense(
                sense.Number ?? fallbackNumber,
                definition?.Value ?? string.Empty,
                CrossReferences(definition?.Id),
                examples);
        }

        PublicDictionaryGramGroup MapGrammarGroup(Segment gramGrp, int fallbackNumber) =>
            new(
                gramGrp.Number ?? fallbackNumber,
                Children(gramGrp.Id, SegmentType.Pos).FirstOrDefault()?.Value,
                CrossReferences(gramGrp.Id),
                Children(gramGrp.Id, SegmentType.Sense)
                    .Select((sense, index) => MapSense(sense, index + 1))
                    .ToList());

        var homonyms = Children(null, SegmentType.Homonym)
            .Select(homonym => new PublicDictionaryHomonym(
                CrossReferences(homonym.Id),
                Children(homonym.Id, SegmentType.GramGrp)
                    .Select((grammarGroup, index) => MapGrammarGroup(grammarGroup, index + 1))
                    .ToList()))
            .ToList();

        var grammarGroups = Children(null, SegmentType.GramGrp)
            .Select((grammarGroup, index) => MapGrammarGroup(grammarGroup, index + 1))
            .ToList();

        var directPoses = Children(null, SegmentType.Pos)
            .Select(pos => new PublicDictionaryDirectPos(
                pos.Value ?? string.Empty,
                CrossReferences(pos.Id),
                Children(pos.Id, SegmentType.Sense)
                    .Select((sense, index) => MapSense(sense, index + 1))
                    .ToList()))
            .ToList();

        var senses = Children(null, SegmentType.Sense)
            .Select((sense, index) => MapSense(sense, index + 1))
            .ToList();

        return new PublicContentGraph(homonyms, grammarGroups, directPoses, senses);
    }

    private static string BuildSnippet(string value, string term)
    {
        const int maxLength = 180;
        if (value.Length <= maxLength)
        {
            return value;
        }

        var index = value.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        var start = Math.Max(0, index - 65);
        start = Math.Min(start, value.Length - maxLength);
        var snippet = value.Substring(start, maxLength).Trim();
        return $"{(start > 0 ? "…" : string.Empty)}{snippet}{(start + maxLength < value.Length ? "…" : string.Empty)}";
    }

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static XrAttributes? ParseXrAttributes(Segment segment)
    {
        if (string.IsNullOrWhiteSpace(segment.Attributes))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<XrAttributes>(segment.Attributes, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record XrAttributes(
        string XrType,
        string Label,
        string Target,
        string? DisplayText,
        int? TargetEntryId = null);

    private sealed record CrossReferenceTarget(int Id, string Lemma, string NormalizedLemma);

    private sealed record PublicContentGraph(
        List<PublicDictionaryHomonym> Homonyms,
        List<PublicDictionaryGramGroup> GramGroups,
        List<PublicDictionaryDirectPos> DirectPoses,
        List<PublicDictionarySense> Senses);
}
