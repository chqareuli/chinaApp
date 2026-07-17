using Chiniseapp.Application.Scoring;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Chiniseapp.Infrastructure.Scoring;

public class ScoringService(ChiniseDbContext db) : IScoringService
{
    public async Task AwardForContentEditAsync(Entry entry, int actingEditorId, CancellationToken ct = default)
    {
        var authorId = entry.MainAuthorEditorId ?? entry.CreatedByEditorId;
        if (actingEditorId != authorId)
        {
            await AwardOnceAsync(entry.Id, actingEditorId, ScoreType.Additional, ct);
        }
    }

    public async Task AwardForStatusChangeAsync(
        Entry entry, EntryStatus previousStatus, EditorRole actingEditorRole, int actingEditorId, CancellationToken ct = default)
    {
        if (previousStatus == EntryStatus.NewEntry && entry.MainAuthorEditorId is int mainAuthorId)
        {
            await AwardOnceAsync(entry.Id, mainAuthorId, ScoreType.Main, ct);
        }

        // A ka_editor completing ka_review work gets the dedicated KaEditor
        // score instead of a generic Additional score for the same action
        // (the "final score type" note in the spec flags this bucket as not
        // fully settled — revisit if the product owner wants it folded into
        // Additional instead).
        if (actingEditorRole == EditorRole.KaEditor && previousStatus == EntryStatus.KaReview)
        {
            await AwardOnceAsync(entry.Id, actingEditorId, ScoreType.KaEditor, ct);
            return;
        }

        var authorId = entry.MainAuthorEditorId ?? entry.CreatedByEditorId;
        if (actingEditorId != authorId)
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
