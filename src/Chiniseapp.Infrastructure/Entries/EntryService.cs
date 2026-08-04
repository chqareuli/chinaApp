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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> AllowedCrossReferenceTypes =
        ["see", "seeAlso", "equivalent", "antonym", "illustration"];
    private static readonly IReadOnlyDictionary<string, string> CrossReferenceLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["see"] = "იხ.",
            ["seeAlso"] = "ასევე.",
            ["equivalent"] = "იგივეა,რაც",
            ["antonym"] = "იხ. ანტ.",
            ["illustration"] = "იხ. სურ.",
        };

    public async Task<EntryDetail> CreateAsync(CreateEntryRequest request, int currentEditorId, CancellationToken ct = default)
    {
        var actor = await GetCurrentEditorAsync(currentEditorId, ct);
        if (!EntryContentRules.CanCreate(actor.Role))
        {
            throw new EntryContentForbiddenException("Your role cannot create entries.");
        }

        var lemma = NormalizeLemma(request.Lemma);

        var pinyins = NormalizePinyins(request.Pinyins, request.Pinyin);
        var now = DateTime.UtcNow;
        var entry = new Entry
        {
            Lemma = lemma,
            Pinyin = pinyins.FirstOrDefault(),
            Status = EntryStatus.NewEntry,
            StatusPriority = EntryStatusPriority.For(EntryStatus.NewEntry),
            CreatedByEditorId = currentEditorId,
            LastEditorEditorId = currentEditorId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            SearchNormalizedTitle = TitleNormalizer.Normalize(lemma),
        };

        for (var index = 0; index < pinyins.Count; index++)
        {
            entry.Pronunciations.Add(new EntryPronunciation
            {
                Value = pinyins[index],
                NormalizedValue = NormalizePinyinForComparison(pinyins[index]),
                OrderIndex = index,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
        }

        db.Entries.Add(entry);
        await db.SaveChangesAsync(ct);

        return await BuildDetailAsync(
            entry,
            EntryContentGraph.Empty,
            await LoadPinyinsAsync(entry.Id, ct),
            await LoadComplexFormsAsync(entry.Id, ct),
            actor.Role,
            ct);
    }

    public async Task<EntryDetail?> GetAsync(int id, int currentEditorId, CancellationToken ct = default)
    {
        var entry = await db.Entries.FindAsync([id], ct);
        if (entry is null)
        {
            return null;
        }

        var actor = await GetCurrentEditorAsync(currentEditorId, ct);
        var content = await LoadContentGraphAsync(id, ct);
        var pinyins = await LoadPinyinsAsync(id, ct);
        var complexForms = await LoadComplexFormsAsync(id, ct);
        return await BuildDetailAsync(entry, content, pinyins, complexForms, actor.Role, ct);
    }

    public async Task<EntryDetail> SaveContentAsync(int id, SaveEntryContentRequest request, int currentEditorId, CancellationToken ct = default)
    {
        var entry = await db.Entries.FindAsync([id], ct)
            ?? throw new EntryNotFoundException(id);
        var actor = await GetCurrentEditorAsync(currentEditorId, ct);

        if (!EntryContentRules.CanEdit(actor.Role, entry.Status))
        {
            throw new EntryContentForbiddenException(
                $"Your role cannot edit content while the entry is in '{entry.Status}' status.");
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

        entry.LastEditorEditorId = currentEditorId;
        entry.UpdatedAtUtc = DateTime.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await ReconcilePronunciationsAsync(entry, request.Pinyins, request.Pinyin, entry.UpdatedAtUtc, ct);
            await ReconcileComplexFormsAsync(entry.Id, request.ComplexForms, entry.UpdatedAtUtc, ct);
            await ReconcileSegmentsAsync(entry.Id, request, entry.UpdatedAtUtc, ct);

            db.AuditLogEntries.Add(new AuditLogEntry
            {
                EntityType = nameof(Entry),
                EntityId = entry.Id,
                Action = AuditAction.ContentEdited,
                PerformedByEditorId = currentEditorId,
                PerformedAtUtc = entry.UpdatedAtUtc,
            });

            await scoringService.AwardForContentEditAsync(entry, currentEditorId, ct);
            await notificationService.NotifyEntryChangedAsync(entry, currentEditorId, nameof(AuditAction.ContentEdited), ct);

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            throw new EntryConcurrencyException();
        }

        return await BuildDetailAsync(
            entry,
            await LoadContentGraphAsync(id, ct),
            await LoadPinyinsAsync(id, ct),
            await LoadComplexFormsAsync(id, ct),
            actor.Role,
            ct);
    }

    public async Task<EntryDetail> ChangeStatusAsync(int id, EntryStatus targetStatus, int currentEditorId, CancellationToken ct = default)
    {
        var entry = await db.Entries.FindAsync([id], ct)
            ?? throw new EntryNotFoundException(id);

        var actor = await GetCurrentEditorAsync(currentEditorId, ct);

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
            await EnsureReadyForPublicationAsync(entry, ct);
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
        await notificationService.NotifyEntryChangedAsync(entry, currentEditorId, nameof(AuditAction.StatusChanged), ct);

        await db.SaveChangesAsync(ct);

        var content = await LoadContentGraphAsync(id, ct);
        return await BuildDetailAsync(
            entry,
            content,
            await LoadPinyinsAsync(id, ct),
            await LoadComplexFormsAsync(id, ct),
            actor.Role,
            ct);
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

    public async Task<PagedResult<EntrySummary>> GetAllListAsync(
        int page,
        int pageSize,
        string? query,
        EntryStatus? status,
        int? editorId,
        EntryListSort sort,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        IQueryable<Entry> entries = db.Entries.AsNoTracking();

        if (status.HasValue)
        {
            entries = entries.Where(entry => entry.Status == status.Value);
        }

        if (editorId.HasValue)
        {
            entries = entries.Where(entry =>
                entry.CreatedByEditorId == editorId.Value ||
                entry.MainAuthorEditorId == editorId.Value ||
                entry.LastEditorEditorId == editorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{EscapeLikePattern(query.Trim())}%";
            entries = entries.Where(entry =>
                EF.Functions.ILike(entry.Lemma, pattern, "\\") ||
                (entry.Pinyin != null && EF.Functions.ILike(entry.Pinyin, pattern, "\\")) ||
                db.EntryPronunciations.Any(item =>
                    item.EntryId == entry.Id && EF.Functions.ILike(item.Value, pattern, "\\")) ||
                db.EntryComplexForms.Any(item =>
                    item.EntryId == entry.Id && EF.Functions.ILike(item.Value, pattern, "\\")));
        }

        var totalCount = await entries.CountAsync(ct);
        var ordered = sort switch
        {
            EntryListSort.UpdatedAsc => entries
                .OrderBy(entry => entry.UpdatedAtUtc)
                .ThenBy(entry => entry.Id),
            EntryListSort.LemmaAsc => entries
                .OrderBy(entry => entry.Lemma)
                .ThenByDescending(entry => entry.UpdatedAtUtc),
            EntryListSort.LemmaDesc => entries
                .OrderByDescending(entry => entry.Lemma)
                .ThenByDescending(entry => entry.UpdatedAtUtc),
            EntryListSort.Status => entries
                .OrderBy(entry => entry.StatusPriority)
                .ThenByDescending(entry => entry.UpdatedAtUtc),
            _ => entries
                .OrderByDescending(entry => entry.UpdatedAtUtc)
                .ThenByDescending(entry => entry.Id),
        };

        var pageEntries = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var names = await LoadDisplayNamesAsync(pageEntries, ct);
        var items = pageEntries.Select(entry => ToSummary(entry, names)).ToList();
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

    public async Task<IReadOnlyList<EntryHistoryItem>> GetHistoryAsync(int id, CancellationToken ct = default)
    {
        var history = await db.AuditLogEntries
            .Where(item => item.EntityType == nameof(Entry) && item.EntityId == id)
            .OrderByDescending(item => item.PerformedAtUtc)
            .ToListAsync(ct);

        if (history.Count == 0)
        {
            return [];
        }

        var editorIds = history.Select(item => item.PerformedByEditorId).Distinct().ToList();
        var names = await db.Editors
            .Where(editor => editorIds.Contains(editor.Id))
            .ToDictionaryAsync(editor => editor.Id, editor => editor.DisplayName, ct);

        return history.Select(item => new EntryHistoryItem(
            item.Id,
            item.Action.ToString(),
            item.PerformedByEditorId,
            names.GetValueOrDefault(item.PerformedByEditorId, string.Empty),
            item.PerformedAtUtc,
            item.OldValue,
            item.NewValue,
            item.Notes)).ToList();
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

    private static List<string> NormalizePinyins(IEnumerable<string>? values, string? legacyValue)
    {
        var source = values?.ToList() ?? [];
        if (source.Count == 0 && !string.IsNullOrWhiteSpace(legacyValue))
        {
            source.Add(legacyValue);
        }

        if (source.Count > 50)
        {
            throw new EntryValidationException("An entry cannot have more than 50 Pinyin values.");
        }

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in source)
        {
            var value = (raw ?? string.Empty).Trim().Normalize();
            if (value.Length == 0)
            {
                throw new EntryValidationException("Pinyin cannot be empty.");
            }

            if (value.Length > 255)
            {
                throw new EntryValidationException("Pinyin must be 255 characters or fewer.");
            }

            var normalized = NormalizePinyinForComparison(value);
            if (!seen.Add(normalized))
            {
                throw new EntryValidationException($"Duplicate Pinyin value '{value}'.");
            }

            result.Add(value);
        }

        return result;
    }

    private static string NormalizePinyinForComparison(string value) =>
        value.Trim().Normalize().ToUpperInvariant();

    private static List<string> NormalizeComplexForms(IEnumerable<string>? values)
    {
        var source = values?.ToList() ?? [];
        if (source.Count > 50)
        {
            throw new EntryValidationException("An entry cannot have more than 50 complex forms.");
        }

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in source)
        {
            var value = (raw ?? string.Empty).Trim().Normalize();
            if (value.Length == 0)
            {
                throw new EntryValidationException("Complex form cannot be empty.");
            }

            if (value.Length > 255)
            {
                throw new EntryValidationException("Complex form must be 255 characters or fewer.");
            }

            var normalized = NormalizeComplexFormForComparison(value);
            if (!seen.Add(normalized))
            {
                throw new EntryValidationException($"Duplicate complex form '{value}'.");
            }

            result.Add(value);
        }

        return result;
    }

    private static string NormalizeComplexFormForComparison(string value) =>
        TitleNormalizer.Normalize(value.Trim().Normalize());

    private async Task ReconcilePronunciationsAsync(
        Entry entry,
        List<PinyinDto> requestedItems,
        string? legacyValue,
        DateTime now,
        CancellationToken ct)
    {
        var existing = await db.EntryPronunciations
            .Where(item => item.EntryId == entry.Id)
            .ToListAsync(ct);
        var byId = existing.ToDictionary(item => item.Id);
        var usedIds = new HashSet<int>();

        var normalizedValues = NormalizePinyins(
            requestedItems.Count > 0 ? requestedItems.Select(item => item.Value) : null,
            legacyValue);

        for (var index = 0; index < normalizedValues.Count; index++)
        {
            var request = requestedItems.Count > 0 ? requestedItems[index] : null;
            EntryPronunciation entity;
            if (request?.Id is int requestedId)
            {
                if (!byId.TryGetValue(requestedId, out entity!) || !usedIds.Add(requestedId))
                {
                    throw new EntryValidationException($"Invalid or duplicate Pinyin id '{requestedId}'.");
                }
            }
            else
            {
                entity = new EntryPronunciation
                {
                    EntryId = entry.Id,
                    CreatedAtUtc = now,
                };
                db.EntryPronunciations.Add(entity);
            }

            entity.Value = normalizedValues[index];
            entity.NormalizedValue = NormalizePinyinForComparison(normalizedValues[index]);
            entity.OrderIndex = index;
            entity.UpdatedAtUtc = now;
        }

        db.EntryPronunciations.RemoveRange(existing.Where(item => !usedIds.Contains(item.Id)));
        entry.Pinyin = normalizedValues.FirstOrDefault();
    }

    private async Task ReconcileComplexFormsAsync(
        int entryId,
        List<ComplexFormDto> requestedItems,
        DateTime now,
        CancellationToken ct)
    {
        var existing = await db.EntryComplexForms
            .Where(item => item.EntryId == entryId)
            .ToListAsync(ct);
        var byId = existing.ToDictionary(item => item.Id);
        var usedIds = new HashSet<int>();
        var normalizedValues = NormalizeComplexForms(requestedItems.Select(item => item.Value));

        for (var index = 0; index < normalizedValues.Count; index++)
        {
            var request = requestedItems[index];
            EntryComplexForm entity;
            if (request.Id is int requestedId)
            {
                if (!byId.TryGetValue(requestedId, out entity!) || !usedIds.Add(requestedId))
                {
                    throw new EntryValidationException($"Invalid or duplicate complex form id '{requestedId}'.");
                }
            }
            else
            {
                entity = new EntryComplexForm
                {
                    EntryId = entryId,
                    CreatedAtUtc = now,
                };
                db.EntryComplexForms.Add(entity);
            }

            entity.Value = normalizedValues[index];
            entity.NormalizedValue = NormalizeComplexFormForComparison(normalizedValues[index]);
            entity.OrderIndex = index;
            entity.UpdatedAtUtc = now;
        }

        db.EntryComplexForms.RemoveRange(existing.Where(item => !usedIds.Contains(item.Id)));
    }

    private async Task ReconcileSegmentsAsync(
        int entryId,
        SaveEntryContentRequest content,
        DateTime now,
        CancellationToken ct)
    {
        var existing = await db.Segments
            .Where(segment => segment.EntryId == entryId)
            .ToListAsync(ct);
        var byId = existing.ToDictionary(segment => segment.Id);
        var usedIds = new HashSet<int>();
        var vocabularies = await db.ControlledVocabularies
            .AsNoTracking()
            .Where(item => item.IsActive)
            .GroupBy(item => item.Category)
            .ToDictionaryAsync(
                group => group.Key,
                group => group.Select(item => item.Code).ToHashSet(StringComparer.Ordinal),
                ct);
        var normalizedCrossReferenceTargets = EnumerateCrossReferences(content)
            .Select(item => TitleNormalizer.Normalize(item.Target.Trim()))
            .Where(target => target.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var crossReferenceTargetCandidates = normalizedCrossReferenceTargets.Count == 0
            ? []
            : await db.Entries
                .AsNoTracking()
                .Where(item =>
                    item.Id != entryId &&
                    normalizedCrossReferenceTargets.Contains(item.SearchNormalizedTitle))
                .Select(item => new CrossReferenceTarget(item.Id, item.Lemma, item.SearchNormalizedTitle, item.Status))
                .ToListAsync(ct);
        var crossReferenceTargets = crossReferenceTargetCandidates
            .GroupBy(item => item.NormalizedLemma, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.Status == EntryStatus.Published)
                    .ThenBy(item => item.Id)
                    .First(),
                StringComparer.Ordinal);

        Segment Upsert(int? requestedId, SegmentType type, Segment? parent, int order)
        {
            Segment segment;
            if (requestedId is int id)
            {
                if (!byId.TryGetValue(id, out segment!) ||
                    segment.SegmentType != type ||
                    !usedIds.Add(id))
                {
                    throw new EntryValidationException($"Invalid {type} segment id '{id}'.");
                }
            }
            else
            {
                segment = new Segment
                {
                    EntryId = entryId,
                    SegmentType = type,
                    CreatedAtUtc = now,
                };
                db.Segments.Add(segment);
            }

            segment.Parent = parent;
            if (parent is null)
            {
                segment.ParentSegmentId = null;
            }
            segment.OrderIndex = order;
            segment.UpdatedAtUtc = now;
            segment.Number = null;
            segment.Value = null;
            segment.Content = null;
            segment.Attributes = null;
            segment.Placement = null;
            return segment;
        }

        static SegmentType MarkerType(VocabularyCategory category) => category switch
        {
            VocabularyCategory.Style => SegmentType.Style,
            VocabularyCategory.Domain => SegmentType.Domain,
            VocabularyCategory.Abbr => SegmentType.Abbr,
            VocabularyCategory.Lang => SegmentType.Lang,
            _ => throw new EntryValidationException("POS is selected on a grammar group, not added as a marker."),
        };

        int AddDecorations(
            Segment target,
            List<MarkerDto> markers,
            List<CrossReferenceDto> crossReferences,
            int startingOrder)
        {
            var order = startingOrder;
            foreach (var marker in markers)
            {
                if (!Enum.TryParse<VocabularyCategory>(marker.Category, true, out var category) ||
                    category == VocabularyCategory.Pos ||
                    !vocabularies.TryGetValue(category, out var allowedValues) ||
                    !allowedValues.Contains(marker.Value))
                {
                    throw new EntryValidationException(
                        $"Unknown {marker.Category} marker '{marker.Value}'.");
                }

                var segment = Upsert(marker.Id, MarkerType(category), target, order++);
                segment.Value = marker.Value;
                segment.Placement = Placement.Attached;
            }

            foreach (var crossReference in crossReferences)
            {
                var xrType = AllowedCrossReferenceTypes.FirstOrDefault(
                    item => string.Equals(item, crossReference.Type, StringComparison.OrdinalIgnoreCase));
                if (xrType is null)
                {
                    throw new EntryValidationException(
                        $"Unknown cross-reference type '{crossReference.Type}'.");
                }

                if (string.IsNullOrWhiteSpace(crossReference.Target))
                {
                    throw new EntryValidationException("A cross-reference target is required.");
                }

                if (!Enum.TryParse<Placement>(crossReference.Placement, true, out var placement) ||
                    !Enum.IsDefined(placement))
                {
                    throw new EntryValidationException(
                        $"Unknown cross-reference placement '{crossReference.Placement}'.");
                }

                var segment = Upsert(crossReference.Id, SegmentType.Xr, target, order++);
                var normalizedTarget = TitleNormalizer.Normalize(crossReference.Target.Trim());
                crossReferenceTargets.TryGetValue(normalizedTarget, out var targetEntry);
                segment.Value = string.IsNullOrWhiteSpace(crossReference.DisplayText)
                    ? crossReference.Target.Trim()
                    : crossReference.DisplayText.Trim();
                segment.Placement = placement;
                segment.Attributes = JsonSerializer.Serialize(
                    new XrAttributes(
                        xrType,
                        CrossReferenceLabels[xrType],
                        crossReference.Target.Trim(),
                        crossReference.DisplayText?.Trim(),
                        targetEntry?.Id),
                    JsonOptions);
            }

            return order;
        }

        var rootKinds = new[]
        {
            content.Homonyms.Count > 0,
            content.GramGroups.Count > 0,
            content.DirectPoses.Count > 0,
            content.Senses.Count > 0,
        }.Count(hasItems => hasItems);
        if (rootKinds > 1)
        {
            throw new EntryValidationException(
                "An entry must use one root structure: Homonym, GramGrp, POS, or Sense.");
        }

        string ValidatePos(string? requestedPos)
        {
            var pos = requestedPos?.Trim() ?? string.Empty;
            if (pos.Length == 0 ||
                !vocabularies.TryGetValue(VocabularyCategory.Pos, out var allowedPos) ||
                !allowedPos.Contains(pos))
            {
                throw new EntryValidationException($"Unknown POS value '{pos}'.");
            }

            return pos;
        }

        void AddSense(SenseDto senseRequest, Segment? parent, int order, int number)
        {
            var sense = Upsert(senseRequest.Id, SegmentType.Sense, parent, order);
            sense.Number = number;

            var definition = Upsert(senseRequest.DefinitionId, SegmentType.Definition, sense, 0);
            definition.Value = senseRequest.Definition.Trim();
            AddDecorations(definition, senseRequest.Markers, senseRequest.CrossReferences, 0);

            for (var e = 0; e < senseRequest.Examples.Count; e++)
            {
                var exampleRequest = senseRequest.Examples[e];
                var example = Upsert(exampleRequest.Id, SegmentType.Example, sense, 1000 + e);

                var zh = Upsert(exampleRequest.ZhSegmentId, SegmentType.ZhSegment, example, 0);
                zh.Value = exampleRequest.Zh.Trim();

                var ka = Upsert(exampleRequest.KaSegmentId, SegmentType.KaSegment, example, 1);
                ka.Value = exampleRequest.Ka.Trim();
                AddDecorations(ka, exampleRequest.Markers, exampleRequest.CrossReferences, 0);
            }
        }

        void AddGrammarGroup(GramGroupDto grammarRequest, Segment? parent, int order, int number)
        {
            var grammar = Upsert(grammarRequest.Id, SegmentType.GramGrp, parent, order);
            grammar.Number = number;

            if (!string.IsNullOrWhiteSpace(grammarRequest.Pos))
            {
                var posSegment = Upsert(grammarRequest.PosSegmentId, SegmentType.Pos, grammar, 0);
                posSegment.Value = ValidatePos(grammarRequest.Pos);
            }
            else if (grammarRequest.PosSegmentId.HasValue)
            {
                throw new EntryValidationException("A POS segment id cannot be saved without a POS value.");
            }

            AddDecorations(grammar, grammarRequest.Markers, grammarRequest.CrossReferences, 10);
            for (var s = 0; s < grammarRequest.Senses.Count; s++)
            {
                AddSense(grammarRequest.Senses[s], grammar, 1000 + s, s + 1);
            }
        }

        for (var h = 0; h < content.Homonyms.Count; h++)
        {
            var homonymRequest = content.Homonyms[h];
            var homonym = Upsert(homonymRequest.Id, SegmentType.Homonym, null, h);
            homonym.Number = h + 1;
            AddDecorations(homonym, homonymRequest.Markers, homonymRequest.CrossReferences, 0);

            for (var g = 0; g < homonymRequest.GramGroups.Count; g++)
            {
                AddGrammarGroup(homonymRequest.GramGroups[g], homonym, 1000 + g, g + 1);
            }
        }

        for (var g = 0; g < content.GramGroups.Count; g++)
        {
            AddGrammarGroup(content.GramGroups[g], null, g, g + 1);
        }

        for (var p = 0; p < content.DirectPoses.Count; p++)
        {
            var directPosRequest = content.DirectPoses[p];
            var pos = Upsert(directPosRequest.Id, SegmentType.Pos, null, p);
            pos.Number = p + 1;
            pos.Value = ValidatePos(directPosRequest.Pos);
            AddDecorations(pos, directPosRequest.Markers, directPosRequest.CrossReferences, 0);
            for (var s = 0; s < directPosRequest.Senses.Count; s++)
            {
                AddSense(directPosRequest.Senses[s], pos, 1000 + s, s + 1);
            }
        }

        for (var s = 0; s < content.Senses.Count; s++)
        {
            AddSense(content.Senses[s], null, s, s + 1);
        }

        db.Segments.RemoveRange(existing.Where(segment => !usedIds.Contains(segment.Id)));
    }

    private async Task<EntryContentGraph> LoadContentGraphAsync(int entryId, CancellationToken ct)
    {
        var segments = await db.Segments
            .Where(s => s.EntryId == entryId)
            .OrderBy(s => s.OrderIndex)
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
                (linkedTargetIds.Contains(item.Id) || normalizedTargets.Contains(item.SearchNormalizedTitle)))
            .Select(item => new CrossReferenceTarget(item.Id, item.Lemma, item.SearchNormalizedTitle, item.Status))
            .ToListAsync(ct);
        var targetEntriesById = targetEntries.ToDictionary(item => item.Id);
        var targetEntriesByNormalized = targetEntries
            .GroupBy(item => item.NormalizedLemma, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.Status == EntryStatus.Published)
                    .ThenBy(item => item.Id)
                    .First(),
                StringComparer.Ordinal);

        var byParent = segments
            .Where(s => s.ParentSegmentId.HasValue)
            .GroupBy(s => s.ParentSegmentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.OrderIndex).ToList());

        IEnumerable<Segment> ChildrenOf(int? parentId) => parentId is null
            ? segments.Where(s => s.ParentSegmentId is null)
            : byParent.TryGetValue(parentId.Value, out var children) ? children : [];

        List<Segment> Children(int? parentId, SegmentType type) =>
            ChildrenOf(parentId).Where(s => s.SegmentType == type).ToList();

        List<MarkerDto> Markers(int parentId) => ChildrenOf(parentId)
            .Where(segment => segment.SegmentType is
                SegmentType.Style or SegmentType.Domain or SegmentType.Abbr or SegmentType.Lang)
            .Select(segment => new MarkerDto
            {
                Id = segment.Id,
                Category = segment.SegmentType.ToString(),
                Value = segment.Value ?? string.Empty,
            })
            .ToList();

        List<CrossReferenceDto> CrossReferences(int parentId) => Children(parentId, SegmentType.Xr)
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

                return new CrossReferenceDto
                {
                    Id = segment.Id,
                    Type = attributes?.XrType ?? "see",
                    Label = attributes?.Label is { Length: > 0 }
                        ? attributes.Label
                        : attributes is not null && CrossReferenceLabels.TryGetValue(attributes.XrType, out var fallbackLabel)
                            ? fallbackLabel
                            : CrossReferenceLabels["see"],
                    Target = attributes?.Target ?? segment.Value ?? string.Empty,
                    TargetEntryId = targetEntry?.Id,
                    TargetEntryLemma = targetEntry?.Lemma,
                    DisplayText = attributes?.DisplayText ?? segment.Value,
                    Placement = (segment.Placement ?? Placement.Standalone).ToString(),
                };
            })
            .ToList();

        SenseDto MapSense(Segment senseSegment, int fallbackNumber)
        {
            var definition = Children(senseSegment.Id, SegmentType.Definition).FirstOrDefault();
            var examples = Children(senseSegment.Id, SegmentType.Example)
                .Select(exampleSegment =>
                {
                    var zh = Children(exampleSegment.Id, SegmentType.ZhSegment).FirstOrDefault();
                    var ka = Children(exampleSegment.Id, SegmentType.KaSegment).FirstOrDefault();
                    return new ExampleDto
                    {
                        Id = exampleSegment.Id,
                        ZhSegmentId = zh?.Id,
                        KaSegmentId = ka?.Id,
                        Zh = zh?.Value ?? string.Empty,
                        Ka = ka?.Value ?? string.Empty,
                        Markers = ka is null ? [] : Markers(ka.Id),
                        CrossReferences = ka is null ? [] : CrossReferences(ka.Id),
                    };
                })
                .ToList();

            return new SenseDto
            {
                Id = senseSegment.Id,
                DefinitionId = definition?.Id,
                Number = senseSegment.Number ?? fallbackNumber,
                Definition = definition?.Value ?? string.Empty,
                Markers = definition is null ? [] : Markers(definition.Id),
                CrossReferences = definition is null ? [] : CrossReferences(definition.Id),
                Examples = examples,
            };
        }

        GramGroupDto MapGrammarGroup(Segment grammarSegment, int fallbackNumber)
        {
            var pos = Children(grammarSegment.Id, SegmentType.Pos).FirstOrDefault();
            var senses = Children(grammarSegment.Id, SegmentType.Sense)
                .Select((sense, index) => MapSense(sense, index + 1))
                .ToList();

            return new GramGroupDto
            {
                Id = grammarSegment.Id,
                PosSegmentId = pos?.Id,
                Number = grammarSegment.Number ?? fallbackNumber,
                Pos = pos?.Value,
                Markers = Markers(grammarSegment.Id),
                CrossReferences = CrossReferences(grammarSegment.Id),
                Senses = senses,
            };
        }

        var homonyms = Children(null, SegmentType.Homonym)
            .Select((homonymSegment, index) => new HomonymDto
            {
                Id = homonymSegment.Id,
                Markers = Markers(homonymSegment.Id),
                CrossReferences = CrossReferences(homonymSegment.Id),
                GramGroups = Children(homonymSegment.Id, SegmentType.GramGrp)
                    .Select((grammarGroup, grammarIndex) => MapGrammarGroup(grammarGroup, grammarIndex + 1))
                    .ToList(),
            })
            .ToList();

        var grammarGroups = Children(null, SegmentType.GramGrp)
            .Select((grammarGroup, index) => MapGrammarGroup(grammarGroup, index + 1))
            .ToList();

        var directPoses = Children(null, SegmentType.Pos)
            .Select((pos, index) => new DirectPosDto
            {
                Id = pos.Id,
                Pos = pos.Value ?? string.Empty,
                Markers = Markers(pos.Id),
                CrossReferences = CrossReferences(pos.Id),
                Senses = Children(pos.Id, SegmentType.Sense)
                    .Select((sense, senseIndex) => MapSense(sense, senseIndex + 1))
                    .ToList(),
            })
            .ToList();

        var senses = Children(null, SegmentType.Sense)
            .Select((sense, index) => MapSense(sense, index + 1))
            .ToList();

        return new EntryContentGraph(homonyms, grammarGroups, directPoses, senses);
    }

    private async Task<List<PinyinDto>> LoadPinyinsAsync(int entryId, CancellationToken ct) =>
        await db.EntryPronunciations
            .AsNoTracking()
            .Where(item => item.EntryId == entryId)
            .OrderBy(item => item.OrderIndex)
            .Select(item => new PinyinDto(item.Id, item.Value, item.OrderIndex + 1))
            .ToListAsync(ct);

    private async Task<List<ComplexFormDto>> LoadComplexFormsAsync(int entryId, CancellationToken ct) =>
        await db.EntryComplexForms
            .AsNoTracking()
            .Where(item => item.EntryId == entryId)
            .OrderBy(item => item.OrderIndex)
            .Select(item => new ComplexFormDto(item.Id, item.Value, item.OrderIndex + 1))
            .ToListAsync(ct);

    private async Task<EntryDetail> BuildDetailAsync(
        Entry entry,
        EntryContentGraph content,
        List<PinyinDto> pinyins,
        List<ComplexFormDto> complexForms,
        EditorRole actorRole,
        CancellationToken ct)
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
            pinyins,
            complexForms,
            entry.Status.ToString(),
            mainAuthorName,
            lastEditorName,
            entry.CreatedAtUtc,
            entry.UpdatedAtUtc,
            rowVersion,
            content.Homonyms,
            content.GramGroups,
            content.DirectPoses,
            content.Senses,
            BuildCapabilities(actorRole, entry.Status));
    }

    private static EntryCapabilities BuildCapabilities(EditorRole role, EntryStatus currentStatus)
    {
        var allowedTransitions = Enum.GetValues<EntryStatus>()
            .Where(target => StatusTransitionRules.CanTransition(role, currentStatus, target))
            .Select(target => target.ToString())
            .ToList();

        return new EntryCapabilities(
            EntryContentRules.CanEdit(role, currentStatus),
            allowedTransitions);
    }

    private async Task EnsureReadyForPublicationAsync(Entry entry, CancellationToken ct)
    {
        var segments = await db.Segments
            .Where(segment => segment.EntryId == entry.Id &&
                (segment.SegmentType == SegmentType.Homonym ||
                 segment.SegmentType == SegmentType.GramGrp ||
                 segment.SegmentType == SegmentType.Pos ||
                 segment.SegmentType == SegmentType.Sense ||
                 segment.SegmentType == SegmentType.Definition))
            .Select(segment => new { segment.SegmentType, segment.Value })
            .ToListAsync(ct);

        var errors = EntryPublicationRules.Validate(
            entry.Lemma,
            segments.Count(segment => segment.SegmentType == SegmentType.Homonym),
            segments.Count(segment => segment.SegmentType == SegmentType.GramGrp),
            segments.Count(segment => segment.SegmentType == SegmentType.Pos),
            segments.Count(segment => segment.SegmentType == SegmentType.Sense),
            segments
                .Where(segment => segment.SegmentType == SegmentType.Definition)
                .Select(segment => segment.Value)
                .ToList());

        if (errors.Count > 0)
        {
            throw new EntryPublicationValidationException(errors);
        }
    }

    private async Task<Editor> GetCurrentEditorAsync(int currentEditorId, CancellationToken ct) =>
        await db.Editors.FindAsync([currentEditorId], ct)
        ?? throw new EntryValidationException("Current editor not found.");

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

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static IEnumerable<CrossReferenceDto> EnumerateCrossReferences(SaveEntryContentRequest content)
    {
        static IEnumerable<CrossReferenceDto> SenseReferences(SenseDto sense)
        {
            foreach (var crossReference in sense.CrossReferences)
            {
                yield return crossReference;
            }
            foreach (var example in sense.Examples)
            {
                foreach (var crossReference in example.CrossReferences)
                {
                    yield return crossReference;
                }
            }
        }

        static IEnumerable<CrossReferenceDto> GrammarReferences(GramGroupDto grammarGroup)
        {
            foreach (var crossReference in grammarGroup.CrossReferences)
            {
                yield return crossReference;
            }
            foreach (var sense in grammarGroup.Senses)
            {
                foreach (var crossReference in SenseReferences(sense))
                {
                    yield return crossReference;
                }
            }
        }

        foreach (var homonym in content.Homonyms)
        {
            foreach (var crossReference in homonym.CrossReferences)
            {
                yield return crossReference;
            }
            foreach (var grammarGroup in homonym.GramGroups)
            {
                foreach (var crossReference in GrammarReferences(grammarGroup))
                {
                    yield return crossReference;
                }
            }
        }
        foreach (var grammarGroup in content.GramGroups)
        {
            foreach (var crossReference in GrammarReferences(grammarGroup))
            {
                yield return crossReference;
            }
        }
        foreach (var directPos in content.DirectPoses)
        {
            foreach (var crossReference in directPos.CrossReferences)
            {
                yield return crossReference;
            }
            foreach (var sense in directPos.Senses)
            {
                foreach (var crossReference in SenseReferences(sense))
                {
                    yield return crossReference;
                }
            }
        }
        foreach (var sense in content.Senses)
        {
            foreach (var crossReference in SenseReferences(sense))
            {
                yield return crossReference;
            }
        }
    }

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
            // Preserve an older/malformed record in editable form instead of hiding it.
            return null;
        }
    }

    private sealed record XrAttributes(
        string XrType,
        string Label,
        string Target,
        string? DisplayText,
        int? TargetEntryId = null);

    private sealed record CrossReferenceTarget(
        int Id,
        string Lemma,
        string NormalizedLemma,
        EntryStatus Status);

    private sealed record EntryContentGraph(
        List<HomonymDto> Homonyms,
        List<GramGroupDto> GramGroups,
        List<DirectPosDto> DirectPoses,
        List<SenseDto> Senses)
    {
        public static EntryContentGraph Empty { get; } = new([], [], [], []);
    }
}
