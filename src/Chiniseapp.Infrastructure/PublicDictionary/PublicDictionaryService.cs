using Chiniseapp.Application.PublicDictionary;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Domain.Rules;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Chiniseapp.Infrastructure.PublicDictionary;

public class PublicDictionaryService(ChiniseDbContext db) : IPublicDictionaryService
{
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
                        item.EntryId == entry.Id && EF.Functions.ILike(item.Value, pinyinPattern, "\\")))
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

        var byParent = segments
            .Where(segment => segment.ParentSegmentId.HasValue)
            .GroupBy(segment => segment.ParentSegmentId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderBy(segment => segment.OrderIndex).ToList());

        IEnumerable<Segment> ChildrenOf(int? parentId) => parentId is null
            ? segments.Where(segment => segment.ParentSegmentId is null)
            : byParent.TryGetValue(parentId.Value, out var children) ? children : [];

        List<Segment> Children(int? parentId, SegmentType type) =>
            ChildrenOf(parentId).Where(segment => segment.SegmentType == type).ToList();

        PublicDictionarySense MapSense(Segment sense, int fallbackNumber)
        {
            var definition = Children(sense.Id, SegmentType.Definition).FirstOrDefault();
            var examples = Children(sense.Id, SegmentType.Example)
                .Select(example => new PublicDictionaryExample(
                    Children(example.Id, SegmentType.ZhSegment).FirstOrDefault()?.Value ?? string.Empty,
                    Children(example.Id, SegmentType.KaSegment).FirstOrDefault()?.Value ?? string.Empty))
                .ToList();
            return new PublicDictionarySense(
                sense.Number ?? fallbackNumber,
                definition?.Value ?? string.Empty,
                examples);
        }

        PublicDictionaryGramGroup MapGrammarGroup(Segment gramGrp, int fallbackNumber) =>
            new(
                gramGrp.Number ?? fallbackNumber,
                Children(gramGrp.Id, SegmentType.Pos).FirstOrDefault()?.Value,
                Children(gramGrp.Id, SegmentType.Sense)
                    .Select((sense, index) => MapSense(sense, index + 1))
                    .ToList());

        var homonyms = Children(null, SegmentType.Homonym)
            .Select(homonym => new PublicDictionaryHomonym(
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

    private sealed record PublicContentGraph(
        List<PublicDictionaryHomonym> Homonyms,
        List<PublicDictionaryGramGroup> GramGroups,
        List<PublicDictionaryDirectPos> DirectPoses,
        List<PublicDictionarySense> Senses);
}
