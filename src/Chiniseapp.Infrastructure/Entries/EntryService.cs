using System.Text.Json;
using Chiniseapp.Application.Entries;
using Chiniseapp.Application.Notifications;
using Chiniseapp.Application.Scoring;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Domain.Rules;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Chiniseapp.Infrastructure.Entries;

public class EntryService(ChiniseDbContext db, IScoringService scoringService, INotificationService notificationService) : IEntryService
{
    public async Task<IReadOnlyList<VocabularyListItem>> GetVocabulariesAsync(CancellationToken ct = default)
    {
        var items = await db.ControlledVocabularies.AsNoTracking().Where(v => v.IsActive)
            .OrderBy(v => v.Category).ThenBy(v => v.SortOrder).ThenBy(v => v.Id).ToListAsync(ct);
        return items.Select(v => new VocabularyListItem(v.Id, v.Category.ToString().ToLowerInvariant(),
            v.Code, v.DisplayZh, v.SortOrder)).ToList();
    }

    public async Task<PagedResult<EntrySummary>> GetFilteredListAsync(
        EntryListFilter filter, EditorRole currentEditorRole, CancellationToken ct = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);
        var query = EntryListQuery.Filter(db.Entries.AsNoTracking(), db.Segments, filter, currentEditorRole);
        if(filter.WorkEditorId is { } workEditor)
        {
            if(filter.ScoreKind is "main" or "additional")
            {
                var type=filter.ScoreKind=="main"?ScoreType.Main:ScoreType.Additional;
                query=query.Where(e=>db.ScoreEntries.Any(s=>s.EntryId==e.Id && s.EditorId==workEditor && s.ScoreType==type));
            }
            else query=query.Where(e=>e.Status!=EntryStatus.Archived && (e.CreatedByEditorId==workEditor || db.ScoreEntries.Any(s=>s.EntryId==e.Id && s.EditorId==workEditor)));
        }
        var total = await query.CountAsync(ct);
        var entries = await EntryListQuery.Sort(query, filter.Sort)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var names = await LoadDisplayNamesAsync(entries, ct);
        return new PagedResult<EntrySummary>(entries.Select(e => ToSummary(e, names)).ToList(), total, page, pageSize);
    }

    public async Task<EntryDetail> CreateAsync(
        CreateEntryRequest request, int currentEditorId, EditorRole currentEditorRole, CancellationToken ct = default)
    {
        if (!EntryPermissionRules.CanCreate(currentEditorRole))
        {
            throw new EntryAccessDeniedException("Your role cannot create entries.");
        }

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

        EntryTreeMapper.SaveForms(entry,
            request.Pinyins?.Select((value, i) => new FormItemDto(null, value, i)).ToList(), null);
        db.Entries.Add(entry);
        await db.SaveChangesAsync(ct);

        return await BuildDetailAsync(entry, [], currentEditorRole, ct);
    }

    public async Task<EntryDetail?> GetAsync(int id, EditorRole currentEditorRole, CancellationToken ct = default)
    {
        var entry = await db.Entries.FindAsync([id], ct);
        if (entry is null)
        {
            return null;
        }

        if (!EntryPermissionRules.CanView(currentEditorRole, entry.Status))
        {
            return null;
        }

        var homonyms = await LoadHomonymsAsync(id, ct);
        return await BuildDetailAsync(entry, homonyms, currentEditorRole, ct);
    }

    public async Task<EntryDetail> SaveContentAsync(
        int id, SaveEntryContentRequest request, int currentEditorId, EditorRole currentEditorRole, CancellationToken ct = default)
    {

        var entry = await db.Entries.FindAsync([id], ct)
            ?? throw new EntryValidationException($"Entry {id} does not exist.");

        if (!EntryPermissionRules.CanEditContent(currentEditorRole, entry.Status))
        {
            throw new EntryAccessDeniedException("Your role cannot edit this entry in its current status.");
        }

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

        EntryTreeMapper.SaveForms(entry, request.Pinyins ?? (request.Pinyin is not null
            ? (string.IsNullOrWhiteSpace(request.Pinyin) ? [] : [new FormItemDto(null, request.Pinyin, 0)]) : null), request.ComplexForms);
        var existing = await db.Segments.Where(s => s.EntryId == id).ToListAsync(ct);
        var mapper = new EntryTreeMapper(id, existing, entry.UpdatedAtUtc);
        mapper.Apply(request);
        db.Segments.AddRange(mapper.Nodes.Where(s => s.Id == 0));

        // Persist moves before removing old ancestors, keeping retained nodes and comments intact.
        // Both phases share the concurrency-checked entry update's transaction.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.AuditLogEntries.Add(new AuditLogEntry
            {
                EntityType = nameof(Entry),
                EntityId = entry.Id,
                Action = AuditAction.ContentEdited,
                PerformedByEditorId = currentEditorId,
                PerformedAtUtc = entry.UpdatedAtUtc,
            });

            await scoringService.AwardForContentEditAsync(entry, currentEditorRole, currentEditorId, ct);
            await notificationService.NotifyEntryChangedAsync(entry, currentEditorId, nameof(AuditAction.ContentEdited), ct);

            await db.SaveChangesAsync(ct);
            db.Segments.RemoveRange(mapper.Removed);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            throw new EntryConcurrencyException();
        }

        return await BuildDetailAsync(entry, await LoadHomonymsAsync(id, ct), currentEditorRole, ct);
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

        if (targetStatus == EntryStatus.Published)
        {
            var tree = EntryTreeMapper.Read(await db.Segments.AsNoTracking().Where(s => s.EntryId == id).ToListAsync(ct));
            var groups = tree.GramGroups.Concat(tree.Homonyms.SelectMany(h => h.GramGroups ?? [])).ToList();
            var senses = tree.Senses.Concat(tree.Homonyms.SelectMany(h=>h.DirectSenses)).Concat(groups.SelectMany(g => g.Senses)).Concat(tree.DirectPoses.SelectMany(p => p.Senses)).ToList();
            if (tree.Homonyms.Any(h => (h.GramGroups is null || h.GramGroups.Count == 0) && h.DirectSenses.Count == 0) ||
                groups.Any(g => string.IsNullOrWhiteSpace(g.Pos) || g.Senses.Count == 0) ||
                tree.DirectPoses.Any(p => string.IsNullOrWhiteSpace(p.Pos) || p.Senses.Count == 0) ||
                senses.Count == 0 || senses.Any(s => string.IsNullOrWhiteSpace(s.Definition)))
                throw new EntryValidationException("Complete the POS, senses and definitions, then save before publishing.");
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

        await scoringService.AwardForStatusChangeAsync(entry, previousStatus, actor.Role, currentEditorId, ct);
        await notificationService.NotifyEntryChangedAsync(entry, currentEditorId, nameof(AuditAction.StatusChanged), ct, $"{previousStatus} → {targetStatus}");

        await db.SaveChangesAsync(ct);

        var homonyms = await LoadHomonymsAsync(id, ct);
        return await BuildDetailAsync(entry, homonyms, actor.Role, ct);
    }

    public async Task<PagedResult<EntrySummary>> GetMainListAsync(
        int page, int pageSize, EditorRole currentEditorRole, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        // Matches Entries(status_priority, updated_at_utc) WHERE status <> 'new_entry'.
        var query = currentEditorRole == EditorRole.KaEditor
            ? db.Entries.Where(e => e.Status == EntryStatus.KaReview)
            : db.Entries.Where(e => e.Status != EntryStatus.NewEntry);

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

    public async Task<IReadOnlyList<EntrySummary>> SearchAsync(
        string query, int limit, EditorRole currentEditorRole, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var normalized = TitleNormalizer.Normalize(query.Trim());
        if (normalized.Length == 0)
        {
            return [];
        }

        // Matches Entries(search_normalized_title) with text_pattern_ops.
        // Includes new_entry (unlike the main list) per the 5.2 search algorithm.
        var entries = db.Entries.AsQueryable();
        if (currentEditorRole == EditorRole.KaEditor)
        {
            entries = entries.Where(e => e.Status == EntryStatus.KaReview);
        }

        var matches = await entries
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

    private async Task<List<HomonymDto>> LoadHomonymsAsync(int entryId, CancellationToken ct) =>
        EntryTreeMapper.Read(await db.Segments.AsNoTracking().Where(s => s.EntryId == entryId).ToListAsync(ct)).Homonyms;

    private async Task<EntryDetail> BuildDetailAsync(
        Entry entry, List<HomonymDto> homonyms, EditorRole currentEditorRole, CancellationToken ct)
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

        var tree = EntryTreeMapper.Read(await db.Segments.AsNoTracking().Where(s => s.EntryId == entry.Id).ToListAsync(ct));
        var forms = EntryTreeMapper.ReadForms(entry);
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
            homonyms,
            new EntryCapabilities(
                EntryPermissionRules.CanEditContent(currentEditorRole, entry.Status),
                Enum.GetValues<EntryStatus>()
                    .Where(target => StatusTransitionRules.CanTransition(currentEditorRole, entry.Status, target))
                    .Select(target => target.ToString())
                    .ToList()))
        {
            Pinyins = forms.Pinyins, ComplexForms = forms.ComplexForms,
            GramGroups = tree.GramGroups, DirectPoses = tree.DirectPoses, Senses = tree.Senses
        };
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
