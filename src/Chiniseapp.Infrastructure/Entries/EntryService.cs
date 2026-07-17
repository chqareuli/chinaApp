using System.Text.Json;
using Chiniseapp.Application.Entries;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Domain.Rules;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Chiniseapp.Infrastructure.Entries;

public class EntryService(ChiniseDbContext db) : IEntryService
{
    public async Task<EntryDetail> CreateAsync(CreateEntryRequest request, int currentEditorId, CancellationToken ct = default)
    {
        var lemma = NormalizeLemma(request.Lemma);

        var now = DateTime.UtcNow;
        var entry = new Entry
        {
            Lemma = lemma,
            Pinyin = NormalizePinyin(request.Pinyin),
            Status = EntryStatus.NewEntry,
            StatusPriority = EntryStatusPriority.For(EntryStatus.NewEntry),
            CreatedByEditorId = currentEditorId,
            LastEditorEditorId = currentEditorId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            SearchNormalizedTitle = TitleNormalizer.Normalize(lemma),
        };

        db.Entries.Add(entry);
        await db.SaveChangesAsync(ct);

        return await BuildDetailAsync(entry, [], ct);
    }

    public async Task<EntryDetail?> GetAsync(int id, CancellationToken ct = default)
    {
        var entry = await db.Entries.FindAsync([id], ct);
        if (entry is null)
        {
            return null;
        }

        var homonyms = await LoadHomonymsAsync(id, ct);
        return await BuildDetailAsync(entry, homonyms, ct);
    }

    public async Task<EntryDetail> SaveContentAsync(int id, SaveEntryContentRequest request, int currentEditorId, CancellationToken ct = default)
    {
        var entry = await db.Entries.FindAsync([id], ct)
            ?? throw new EntryValidationException($"Entry {id} does not exist.");

        // The xmin shadow property drives EF Core's optimistic-concurrency
        // check: setting OriginalValue makes the generated UPDATE include
        // "WHERE xmin = <what the client last read>", so a concurrent save
        // since then makes this UPDATE affect 0 rows and throw below.
        db.Entry(entry).Property<uint>("xmin").OriginalValue = request.RowVersion;

        if (!string.IsNullOrWhiteSpace(request.Lemma))
        {
            entry.Lemma = NormalizeLemma(request.Lemma);
            entry.SearchNormalizedTitle = TitleNormalizer.Normalize(entry.Lemma);
        }

        if (request.Pinyin is not null)
        {
            entry.Pinyin = NormalizePinyin(request.Pinyin);
        }

        entry.LastEditorEditorId = currentEditorId;
        entry.UpdatedAtUtc = DateTime.UtcNow;

        // Delete-then-recreate needs to share a transaction with the
        // concurrency-checked entry update: ExecuteDeleteAsync runs
        // immediately (not deferred to SaveChanges), so without an explicit
        // transaction a concurrency conflict on the entry row would still
        // leave the old segments deleted.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.Segments.Where(s => s.EntryId == id).ExecuteDeleteAsync(ct);

            var newSegments = new List<Segment>();
            BuildSegments(newSegments, id, request.Homonyms, entry.UpdatedAtUtc);
            db.Segments.AddRange(newSegments);

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            throw new EntryConcurrencyException();
        }

        return await BuildDetailAsync(entry, request.Homonyms, ct);
    }

    public async Task<EntryDetail> ChangeStatusAsync(int id, EntryStatus targetStatus, int currentEditorId, CancellationToken ct = default)
    {
        var entry = await db.Entries.FindAsync([id], ct)
            ?? throw new EntryValidationException($"Entry {id} does not exist.");

        var actor = await db.Editors.FindAsync([currentEditorId], ct)
            ?? throw new EntryValidationException("Current editor not found.");

        var previousStatus = entry.Status;
        if (previousStatus == targetStatus)
        {
            throw new EntryValidationException("Entry is already in that status.");
        }

        if (!StatusTransitionRules.CanTransition(actor.Role, previousStatus, targetStatus))
        {
            throw new EntryStatusTransitionForbiddenException(previousStatus.ToString(), targetStatus.ToString());
        }

        // main_author locks the first time an entry leaves new_entry, to the
        // entry's original creator — this already covers the assistant_editor
        // case (an assistant-authored entry promoted by a supervisor keeps
        // the assistant as main_author, per the resolved Q8 decision) without
        // needing special-case logic, since CreatedByEditorId never changes.
        if (previousStatus == EntryStatus.NewEntry && entry.MainAuthorEditorId is null)
        {
            entry.MainAuthorEditorId = entry.CreatedByEditorId;
        }

        entry.Status = targetStatus;
        entry.StatusPriority = EntryStatusPriority.For(targetStatus);
        entry.LastEditorEditorId = currentEditorId;
        entry.UpdatedAtUtc = DateTime.UtcNow;

        // Scoring (M6) and notifications (M7) hook into this same event once
        // built; for now this only records the transition for the audit trail.
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            EntityType = nameof(Entry),
            EntityId = entry.Id,
            Action = AuditAction.StatusChanged,
            PerformedByEditorId = currentEditorId,
            PerformedAtUtc = entry.UpdatedAtUtc,
            OldValue = JsonSerializer.Serialize(new { status = previousStatus.ToString() }),
            NewValue = JsonSerializer.Serialize(new { status = targetStatus.ToString() }),
        });

        await db.SaveChangesAsync(ct);

        var homonyms = await LoadHomonymsAsync(id, ct);
        return await BuildDetailAsync(entry, homonyms, ct);
    }

    public async Task<PagedResult<EntrySummary>> GetMainListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        // Matches Entries(status_priority, updated_at_utc) WHERE status <> 'new_entry'.
        var query = db.Entries.Where(e => e.Status != EntryStatus.NewEntry);

        var totalCount = await query.CountAsync(ct);
        var page1 = await query
            .OrderBy(e => e.StatusPriority)
            .ThenByDescending(e => e.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var names = await LoadDisplayNamesAsync(page1, ct);
        var items = page1.Select(e => ToSummary(e, names)).ToList();
        return new PagedResult<EntrySummary>(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<EntrySummary>> SearchAsync(string query, int limit, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var normalized = TitleNormalizer.Normalize(query.Trim());
        if (normalized.Length == 0)
        {
            return [];
        }

        // Matches Entries(search_normalized_title) with text_pattern_ops.
        // Includes new_entry (unlike the main list) per the 5.2 search algorithm.
        var matches = await db.Entries
            .Where(e => EF.Functions.Like(e.SearchNormalizedTitle, normalized + "%"))
            .OrderBy(e => e.Lemma.Length)
            .ThenBy(e => e.StatusPriority)
            .ThenByDescending(e => e.UpdatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);

        var names = await LoadDisplayNamesAsync(matches, ct);
        return matches.Select(e => ToSummary(e, names)).ToList();
    }

    private static string NormalizeLemma(string? lemma)
    {
        var trimmed = lemma?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new EntryValidationException("Lemma (headword) is required.");
        }

        if (trimmed.Length > 255)
        {
            throw new EntryValidationException("Lemma must be 255 characters or fewer.");
        }

        return trimmed;
    }

    private static string? NormalizePinyin(string? pinyin) =>
        string.IsNullOrWhiteSpace(pinyin) ? null : pinyin.Trim();

    private static void BuildSegments(List<Segment> target, int entryId, List<HomonymDto> homonyms, DateTime now)
    {
        for (var h = 0; h < homonyms.Count; h++)
        {
            var homonymSegment = NewSegment(entryId, null, SegmentType.Homonym, h, now);
            target.Add(homonymSegment);

            // Stage 1 hides gramGrp/POS from the editor: every homonym gets
            // exactly one implicit gramGrp so "sense belongs to a gramGrp"
            // holds without exposing that UI yet (per the Stage-1 tree-shape
            // rule in docs/backend-plan.md).
            var gramGrpSegment = NewSegment(entryId, homonymSegment, SegmentType.GramGrp, 0, now);
            target.Add(gramGrpSegment);

            var senses = homonyms[h].Senses;
            for (var s = 0; s < senses.Count; s++)
            {
                var senseSegment = NewSegment(entryId, gramGrpSegment, SegmentType.Sense, s, now);
                senseSegment.Number = s + 1;
                target.Add(senseSegment);

                var definitionSegment = NewSegment(entryId, senseSegment, SegmentType.Definition, 0, now);
                definitionSegment.Value = senses[s].Definition;
                target.Add(definitionSegment);

                var examples = senses[s].Examples;
                for (var e = 0; e < examples.Count; e++)
                {
                    var exampleSegment = NewSegment(entryId, senseSegment, SegmentType.Example, e + 1, now);
                    target.Add(exampleSegment);

                    var zhSegment = NewSegment(entryId, exampleSegment, SegmentType.ZhSegment, 0, now);
                    zhSegment.Value = examples[e].Zh;
                    target.Add(zhSegment);

                    var kaSegment = NewSegment(entryId, exampleSegment, SegmentType.KaSegment, 1, now);
                    kaSegment.Value = examples[e].Ka;
                    target.Add(kaSegment);
                }
            }
        }
    }

    private static Segment NewSegment(int entryId, Segment? parent, SegmentType type, int order, DateTime now) => new()
    {
        EntryId = entryId,
        Parent = parent,
        SegmentType = type,
        OrderIndex = order,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    private async Task<List<HomonymDto>> LoadHomonymsAsync(int entryId, CancellationToken ct)
    {
        var segments = await db.Segments
            .Where(s => s.EntryId == entryId)
            .OrderBy(s => s.OrderIndex)
            .ToListAsync(ct);

        var byParent = segments
            .Where(s => s.ParentSegmentId.HasValue)
            .GroupBy(s => s.ParentSegmentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.OrderIndex).ToList());

        IEnumerable<Segment> ChildrenOf(int? parentId) => parentId is null
            ? segments.Where(s => s.ParentSegmentId is null)
            : byParent.TryGetValue(parentId.Value, out var children) ? children : [];

        List<Segment> Children(int? parentId, SegmentType type) =>
            ChildrenOf(parentId).Where(s => s.SegmentType == type).ToList();

        var homonyms = new List<HomonymDto>();
        foreach (var homonymSeg in Children(null, SegmentType.Homonym))
        {
            var gramGrp = Children(homonymSeg.Id, SegmentType.GramGrp).FirstOrDefault();
            var senseDtos = new List<SenseDto>();

            if (gramGrp is not null)
            {
                foreach (var senseSeg in Children(gramGrp.Id, SegmentType.Sense))
                {
                    var definition = Children(senseSeg.Id, SegmentType.Definition).FirstOrDefault();
                    var exampleDtos = Children(senseSeg.Id, SegmentType.Example)
                        .Select(exampleSeg => new ExampleDto(
                            Children(exampleSeg.Id, SegmentType.ZhSegment).FirstOrDefault()?.Value ?? string.Empty,
                            Children(exampleSeg.Id, SegmentType.KaSegment).FirstOrDefault()?.Value ?? string.Empty))
                        .ToList();

                    senseDtos.Add(new SenseDto(senseSeg.Number ?? 0, definition?.Value ?? string.Empty, exampleDtos));
                }
            }

            homonyms.Add(new HomonymDto(senseDtos));
        }

        return homonyms;
    }

    private async Task<EntryDetail> BuildDetailAsync(Entry entry, List<HomonymDto> homonyms, CancellationToken ct)
    {
        string? mainAuthorName = null;
        if (entry.MainAuthorEditorId is int mainAuthorId)
        {
            mainAuthorName = await db.Editors
                .Where(e => e.Id == mainAuthorId)
                .Select(e => e.DisplayName)
                .FirstOrDefaultAsync(ct);
        }

        var lastEditorName = await db.Editors
            .Where(e => e.Id == entry.LastEditorEditorId)
            .Select(e => e.DisplayName)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var rowVersion = db.Entry(entry).Property<uint>("xmin").CurrentValue;

        return new EntryDetail(
            entry.Id,
            entry.Lemma,
            entry.Pinyin,
            entry.Status.ToString(),
            mainAuthorName,
            lastEditorName,
            entry.CreatedAtUtc,
            entry.UpdatedAtUtc,
            rowVersion,
            homonyms);
    }

    private async Task<Dictionary<int, string>> LoadDisplayNamesAsync(List<Entry> entries, CancellationToken ct)
    {
        var editorIds = entries
            .Select(e => e.LastEditorEditorId)
            .Concat(entries.Where(e => e.MainAuthorEditorId.HasValue).Select(e => e.MainAuthorEditorId!.Value))
            .Distinct()
            .ToList();

        return await db.Editors
            .Where(e => editorIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.DisplayName, ct);
    }

    private static EntrySummary ToSummary(Entry entry, Dictionary<int, string> names) => new(
        entry.Id,
        entry.Lemma,
        entry.Pinyin,
        entry.Status.ToString(),
        entry.MainAuthorEditorId is int id && names.TryGetValue(id, out var mainName) ? mainName : null,
        names.GetValueOrDefault(entry.LastEditorEditorId, string.Empty),
        entry.UpdatedAtUtc);
}
