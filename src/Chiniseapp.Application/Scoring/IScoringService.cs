using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Application.Scoring;

/// <summary>
/// 10.1 scoring rules. Callers stage score entries on the shared DbContext and
/// let the caller's own SaveChangesAsync persist them alongside whatever
/// entry/audit changes triggered the award, so one request is one transaction.
/// </summary>
public interface IScoringService
{
    /// <summary>
    /// Call after a content save. Awards Additional score to the acting editor
    /// once, if they aren't the entry's (provisional or locked) author.
    /// </summary>
    Task AwardForContentEditAsync(Entry entry, int actingEditorId, CancellationToken ct = default);

    /// <summary>
    /// Call after a status change, after MainAuthorEditorId has been finalized
    /// on <paramref name="entry"/> for this transition. Awards Main score
    /// (once, the first time an entry leaves new_entry), a KaEditor score
    /// (once, when a ka_editor completes ka_review work), or otherwise an
    /// Additional score to the acting editor if they aren't the author.
    /// </summary>
    Task AwardForStatusChangeAsync(
        Entry entry, EntryStatus previousStatus, EditorRole actingEditorRole, int actingEditorId, CancellationToken ct = default);
}
