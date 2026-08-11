using Chiniseapp.Application.Scoring;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Domain.Rules;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Chiniseapp.Infrastructure.Scoring;

public class ScoringService(ChiniseDbContext db) : IScoringService
{
    public async Task AwardForContentEditAsync(
        Entry entry, EditorRole actingEditorRole, int actingEditorId, CancellationToken ct = default)
    {
        var authorId = entry.MainAuthorEditorId ?? entry.CreatedByEditorId;
        if (!EntryPermissionRules.CanEarnMainScore(actingEditorRole) || actingEditorId != authorId)
        {
            await AwardOnceAsync(entry.Id, actingEditorId, ScoreType.Additional, ct);
        }
    }

    public async Task AwardForStatusChangeAsync(
        Entry entry, EntryStatus previousStatus, EditorRole actingEditorRole, int actingEditorId, CancellationToken ct = default)
    {
        if (previousStatus == EntryStatus.NewEntry && entry.MainAuthorEditorId is int mainAuthorId)
        {
            var mainAuthorRole = await db.Editors
                .Where(e => e.Id == mainAuthorId)
                .Select(e => e.Role)
                .SingleAsync(ct);
            if (EntryPermissionRules.CanEarnMainScore(mainAuthorRole))
            {
                await AwardOnceAsync(entry.Id, mainAuthorId, ScoreType.Main, ct);
            }
        }

        var authorId = entry.MainAuthorEditorId ?? entry.CreatedByEditorId;
        if (!EntryPermissionRules.CanEarnMainScore(actingEditorRole) || actingEditorId != authorId)
        {
            await AwardOnceAsync(entry.Id, actingEditorId, ScoreType.Additional, ct);
        }
    }

    private async Task AwardOnceAsync(int entryId, int editorId, ScoreType scoreType, CancellationToken ct)
    {
        // The unique index on (entry_id, editor_id, score_type) is the real
        // guarantee against double-awarding; this check is just the
        // common-case fast path within a single request/transaction.
        var alreadyAwarded = await db.ScoreEntries.AnyAsync(
            s => s.EntryId == entryId && s.EditorId == editorId && s.ScoreType == scoreType, ct);
        if (alreadyAwarded)
        {
            return;
        }

        db.ScoreEntries.Add(new ScoreEntry
        {
            EntryId = entryId,
            EditorId = editorId,
            ScoreType = scoreType,
            AwardedAtUtc = DateTime.UtcNow,
        });
    }
}
