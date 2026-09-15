using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Domain.Rules;
using System.Text;

namespace Chiniseapp.Application.Entries;

public static class EntryListQuery
{
    public static bool CanSearchContent(string value)
    {
        var letters = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.Value is >= 0x3400 and <= 0x4dbf or >= 0x4e00 and <= 0x9fff or >= 0x20000 and <= 0x2fa1f)
                return true;
            if (Rune.IsLetter(rune) && ++letters >= 2) return true;
        }
        return false;
    }

    public static IQueryable<Entry> Filter(IQueryable<Entry> entries, IQueryable<Segment> segments,
        EntryListFilter filter, EditorRole role)
    {
        var query = entries;
        if (role == EditorRole.KaEditor) query = query.Where(e => e.Status == EntryStatus.KaReview);
        if (filter.Status is { } status) query = query.Where(e => e.Status == status);
        if (filter.ExcludeDrafts) query = query.Where(e => e.Status != EntryStatus.NewEntry);
        if (filter.EditorId is { } editorId) query = query.Where(e => e.LastEditorEditorId == editorId);
        if (filter.AuthorId is { } authorId) query = query.Where(e => e.MainAuthorEditorId == authorId);
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var term = filter.Query.Trim();
            var normalized = TitleNormalizer.Normalize(term);
            query = query.Where(e => e.SearchNormalizedTitle.Contains(normalized)
                || (e.Pinyin != null && e.Pinyin.Contains(term))
                || segments.Any(s => s.EntryId == e.Id && s.Value != null && s.Value.Contains(term)));
        }
        return query;
    }

    public static IOrderedQueryable<Entry> Sort(IQueryable<Entry> query, string sort) => (sort switch
    {
        "UpdatedAsc" => query.OrderBy(e => e.UpdatedAtUtc),
        "LemmaAsc" => query.OrderBy(e => e.Lemma),
        "LemmaDesc" => query.OrderByDescending(e => e.Lemma),
        "Status" => query.OrderBy(e => e.StatusPriority).ThenByDescending(e => e.UpdatedAtUtc),
        _ => query.OrderByDescending(e => e.UpdatedAtUtc),
    }).ThenBy(e => e.Id);
}
